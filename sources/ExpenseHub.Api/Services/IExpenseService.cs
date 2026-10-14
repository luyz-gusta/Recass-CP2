using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Reembolsos do próprio usuário (criar, editar e enviar rascunhos) e consulta conforme o perfil (listar e detalhar).
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

    /// <summary>
    /// Envia um rascunho do próprio ator para aprovação (Draft para Submitted), com o histórico do envio, numa única gravação.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// O reembolso enviado; Forbidden se o ator não for Employee; NotFound se o reembolso não existir ou for de outra pessoa;
    /// Conflict se já tiver sido enviado (não há reenvio) ou houver conflito de concorrência.
    /// </returns>
    Task<ServiceResult<ExpenseResponse>> SubmitAsync(Actor actor, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Lista os reembolsos visíveis ao ator, do mais recente para o mais antigo. O filtro de visibilidade é aplicado na consulta ao banco.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Os reembolsos visíveis; Forbidden se o ator não tiver nenhuma role de leitura (por exemplo, Admin sozinho).</returns>
    Task<ServiceResult<IReadOnlyList<ExpenseResponse>>> ListAsync(Actor actor, CancellationToken cancellationToken);

    /// <summary>
    /// Consulta um reembolso visível ao ator.
    /// </summary>
    /// <param name="actor">Usuário autenticado.</param>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// O reembolso; Forbidden se o ator não tiver nenhuma role de leitura; NotFound se o reembolso não existir
    /// ou estiver fora do escopo de leitura do ator (a resposta é a mesma nos dois casos).
    /// </returns>
    Task<ServiceResult<ExpenseResponse>> GetAsync(Actor actor, Guid id, CancellationToken cancellationToken);
}
