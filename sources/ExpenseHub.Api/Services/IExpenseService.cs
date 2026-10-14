using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Ciclo de vida dos reembolsos do próprio usuário: criação e edição de rascunhos.
/// A regra de decisão é sempre a mesma: role (403), entrada (400), escopo (404), estado (409) e persistência.
/// </summary>
public interface IExpenseService
{
    /// <summary>
    /// Cria um reembolso em Draft cujo proprietário é o ator autenticado, com o histórico de criação, numa única gravação.
    /// </summary>
    /// <param name="actor">Usuário autenticado; vira o proprietário.</param>
    /// <param name="request">Dados da despesa.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// O rascunho criado; Forbidden se o ator não for Employee; Validation para data futura ou categoria inexistente.
    /// </returns>
    Task<ServiceResult<ExpenseResponse>> CreateAsync(Actor actor, CreateExpenseRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Edita um rascunho do próprio ator. Mudanças geram histórico com o detalhe dos campos alterados; uma edição sem mudanças não gera histórico.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="request">Novos dados da despesa.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// O rascunho atualizado; Forbidden se o ator não for Employee; Validation para data futura ou categoria inexistente;
    /// NotFound se o reembolso não existir ou for de outra pessoa; Conflict se não estiver em Draft ou houver conflito de concorrência.
    /// </returns>
    Task<ServiceResult<ExpenseResponse>> UpdateAsync(Actor actor, Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken);
}
