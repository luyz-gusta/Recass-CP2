using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Decisões sobre reembolsos de outras pessoas (aprovar e reprovar).
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
}
