using System;
using System.Linq.Expressions;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Regras de acesso a reembolsos que combinam role, ownership e estado.
/// Recebe dados primitivos (e não a entidade), então qualquer combinação é testável sem banco.
/// </summary>
internal static class ExpenseAccessPolicy
{
    /// <summary>
    /// Indica se o ator tem alguma role de leitura de reembolsos (Employee, Approver, Finance ou Auditor).
    /// Admin, sozinho, e usuário sem role não leem reembolsos.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <returns><see langword="true"/> quando o ator possui ao menos uma role de leitura.</returns>
    public static bool HasReadRole(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return actor.IsInRole(AppRoles.Employee)
            || actor.IsInRole(AppRoles.Approver)
            || actor.IsInRole(AppRoles.Finance)
            || actor.IsInRole(AppRoles.Auditor);
    }

    /// <summary>
    /// Filtro de visibilidade de leitura (lista, detalhe e histórico), pensado para ser aplicado na consulta, antes de materializar os dados.
    /// Com roles acumuladas vale a união das permissões: Auditor vê tudo; Employee vê os próprios; Approver vê os enviados
    /// (<see cref="ExpenseStatus.Submitted"/>); Finance vê os aprovados e os pagos. Sem role de leitura, nada é visível.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <returns>Expressão traduzível para SQL.</returns>
    public static Expression<Func<Expense, bool>> ReadScope(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // Valores simples calculados fora da expressão: o EF os trata como parâmetros e a consulta continua traduzível para SQL.
        bool isAuditor = actor.IsInRole(AppRoles.Auditor);
        bool isEmployee = actor.IsInRole(AppRoles.Employee);
        bool isApprover = actor.IsInRole(AppRoles.Approver);
        bool isFinance = actor.IsInRole(AppRoles.Finance);
        string userId = actor.UserId;

        return expense => isAuditor
            || (isEmployee && expense.OwnerId == userId)
            || (isApprover && expense.Status == ExpenseStatus.Submitted)
            || (isFinance && (expense.Status == ExpenseStatus.Approved || expense.Status == ExpenseStatus.Paid));
    }

    /// <summary>
    /// Decide se o ator pode editar ou enviar um rascunho do proprietário informado.
    /// Sem a role Employee é Forbidden; reembolso de outra pessoa é NotFound, para não revelar que existe.
    /// Roles acumuladas não ampliam o acesso: só o proprietário com a role Employee passa.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="ownerId">Identificador do proprietário do reembolso.</param>
    /// <returns>A decisão de acesso.</returns>
    public static AccessDecision CanManageDraft(Actor actor, string ownerId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(ownerId);

        if (!actor.IsInRole(AppRoles.Employee))
        {
            return AccessDecision.Forbidden;
        }

        if (!string.Equals(actor.UserId, ownerId, StringComparison.Ordinal))
        {
            return AccessDecision.NotFound;
        }

        return AccessDecision.Allowed;
    }

    /// <summary>
    /// Decide se o ator pode aprovar ou reprovar um reembolso. A decisão avalia, nesta ordem fixa:
    /// (1) sem a role Approver é Forbidden; (2) rascunho de outra pessoa é NotFound, porque o Approver nem enxerga rascunhos;
    /// (3) reembolso do próprio ator é Forbidden, mesmo acumulando roles (autoaprovação proibida); (4) caso contrário, Allowed.
    /// O estado do reembolso só entra para ocultar rascunhos alheios: a transição válida (somente Submitted) é verificada
    /// depois pelo serviço, que responde 409 sem gravar histórico.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="ownerId">Identificador do proprietário do reembolso.</param>
    /// <param name="status">Estado atual do reembolso.</param>
    /// <returns>A decisão de acesso.</returns>
    public static AccessDecision CanDecide(Actor actor, string ownerId, ExpenseStatus status)
    {
        return Evaluate(actor, AppRoles.Approver, ownerId, status);
    }

    /// <summary>
    /// Decide se o ator pode registrar o pagamento de um reembolso. A decisão avalia, nesta ordem fixa:
    /// (1) sem a role Finance é Forbidden; (2) rascunho de outra pessoa é NotFound, porque o Finance nem enxerga rascunhos;
    /// (3) reembolso do próprio ator é Forbidden, mesmo acumulando roles (autopagamento proibido); (4) caso contrário, Allowed.
    /// O estado do reembolso só entra para ocultar rascunhos alheios: a transição válida (somente Approved) é verificada
    /// depois pelo serviço, que responde 409 sem gravar histórico nem pagamento.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="ownerId">Identificador do proprietário do reembolso.</param>
    /// <param name="status">Estado atual do reembolso.</param>
    /// <returns>A decisão de acesso.</returns>
    public static AccessDecision CanPay(Actor actor, string ownerId, ExpenseStatus status)
    {
        return Evaluate(actor, AppRoles.Finance, ownerId, status);
    }

    private static AccessDecision Evaluate(Actor actor, string requiredRole, string ownerId, ExpenseStatus status)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(ownerId);

        if (!actor.IsInRole(requiredRole))
        {
            return AccessDecision.Forbidden;
        }

        bool isOwner = string.Equals(actor.UserId, ownerId, StringComparison.Ordinal);

        if (!isOwner && status == ExpenseStatus.Draft)
        {
            return AccessDecision.NotFound;
        }

        if (isOwner)
        {
            return AccessDecision.Forbidden;
        }

        return AccessDecision.Allowed;
    }
}
