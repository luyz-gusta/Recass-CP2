using System;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Regras de acesso a reembolsos que combinam role, ownership e estado.
/// Recebe dados primitivos (e não a entidade), então qualquer combinação é testável sem banco.
/// </summary>
internal static class ExpenseAccessPolicy
{
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
}
