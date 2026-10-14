using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Fluxo de reembolsos de outras pessoas (aprovar, reprovar e pagar) e consulta do histórico.
/// A regra de decisão é sempre a mesma: role (403), entrada (400), escopo (404), ownership (403), estado (409) e persistência.
/// </summary>
public interface IExpenseWorkflowService
{
    /// <summary>
    /// Aprova um reembolso enviado de outra pessoa (Submitted para Approved), com o histórico da decisão, numa única gravação.
    /// O aprovador e o horário vêm do ator autenticado e do servidor.
    /// </summary>
    /// <param name="actor">Usuário autenticado; vira o aprovador.</param>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// O reembolso aprovado; Forbidden se o ator não for Approver ou for o proprietário; NotFound se o reembolso não existir
    /// ou for o rascunho de outra pessoa; Conflict se não estiver em Submitted (inclusive uma decisão repetida)
    /// ou houver conflito de concorrência.
    /// </returns>
    Task<ServiceResult<ExpenseResponse>> ApproveAsync(Actor actor, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Reprova um reembolso enviado de outra pessoa (Submitted para Rejected). A justificativa, sem os espaços nas pontas,
    /// é gravada no reembolso e no histórico, numa única gravação. O aprovador e o horário vêm do ator autenticado e do servidor.
    /// </summary>
    /// <param name="actor">Usuário autenticado; vira o aprovador.</param>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="request">Justificativa da reprovação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// O reembolso reprovado; Forbidden se o ator não for Approver ou for o proprietário; Validation se a justificativa
    /// for nula, vazia ou tiver menos de 10 ou mais de 500 caracteres depois de remover os espaços nas pontas; NotFound se o
    /// reembolso não existir ou for o rascunho de outra pessoa; Conflict se não estiver em Submitted (inclusive uma decisão
    /// repetida) ou houver conflito de concorrência.
    /// </returns>
    Task<ServiceResult<ExpenseResponse>> RejectAsync(Actor actor, Guid id, RejectExpenseRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Registra o pagamento de um reembolso aprovado de outra pessoa (Approved para Paid). A mudança de estado, o registro
    /// de pagamento e o histórico são gravados juntos, numa única gravação: ou tudo é persistido, ou nada.
    /// Quem pagou, o valor e o horário vêm do ator autenticado, do reembolso e do servidor.
    /// </summary>
    /// <param name="actor">Usuário autenticado; vira quem pagou.</param>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// O reembolso pago; Forbidden se o ator não for Finance ou for o proprietário; NotFound se o reembolso não existir
    /// ou for o rascunho de outra pessoa; Conflict se não estiver em Approved (inclusive um segundo pagamento)
    /// ou houver conflito de concorrência.
    /// </returns>
    Task<ServiceResult<ExpenseResponse>> PayAsync(Actor actor, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista o histórico de um reembolso em ordem cronológica, com a mesma visibilidade do detalhe: o filtro de leitura
    /// do ator é aplicado na consulta.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// As entradas do histórico; Forbidden se o ator não tiver nenhuma role de leitura; NotFound se o reembolso não existir
    /// ou estiver fora do escopo de leitura do ator (a resposta é a mesma nos dois casos).
    /// </returns>
    Task<ServiceResult<IReadOnlyList<ExpenseHistoryResponse>>> GetHistoryAsync(Actor actor, Guid id, CancellationToken cancellationToken);
}
