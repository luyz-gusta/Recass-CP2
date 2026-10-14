using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Http;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Reembolsos do usuário autenticado. A role é verificada aqui; ownership e estado são decididos na camada de serviço.
/// </summary>
[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    /// <summary>
    /// Cria o controller de reembolsos.
    /// </summary>
    /// <param name="expenseService">Serviço de reembolsos.</param>
    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Cria um reembolso em Draft. O proprietário é o usuário autenticado; proprietário, estado e horários não são aceitos no corpo.
    /// </summary>
    /// <param name="request">Descrição, valor, data da despesa e categoria.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>201 com o rascunho criado; 400 para dados inválidos; 403 sem a role Employee.</returns>
    [HttpPost]
    [Authorize(Roles = AppRoles.Employee)]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<ExpenseResponse> result = await _expenseService.CreateAsync(User.ToActor(), request, cancellationToken);
        return this.ToActionResult(result, expense => Created($"/api/expenses/{expense.Id}", expense));
    }

    /// <summary>
    /// Edita um rascunho do próprio usuário. Reembolso de outra pessoa responde 404; fora de Draft responde 409.
    /// </summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="request">Novos descrição, valor, data da despesa e categoria.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>200 com o rascunho atualizado; 400 para dados inválidos; 403 sem a role Employee; 404 para reembolso inexistente ou alheio; 409 fora de Draft.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Employee)]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(Guid id, UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<ExpenseResponse> result = await _expenseService.UpdateAsync(User.ToActor(), id, request, cancellationToken);
        return this.ToActionResult(result, expense => Ok(expense));
    }

    /// <summary>
    /// Envia um rascunho do próprio usuário para aprovação (Draft para Submitted). Repetir o envio responde 409.
    /// </summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>200 com o reembolso enviado; 403 sem a role Employee; 404 para reembolso inexistente ou alheio; 409 se já foi enviado.</returns>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = AppRoles.Employee)]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitAsync(Guid id, CancellationToken cancellationToken)
    {
        ServiceResult<ExpenseResponse> result = await _expenseService.SubmitAsync(User.ToActor(), id, cancellationToken);
        return this.ToActionResult(result, expense => Ok(expense));
    }

    /// <summary>
    /// Lista os reembolsos conforme o perfil: Employee vê os próprios, Approver os enviados, Finance os aprovados e pagos e Auditor todos.
    /// Roles acumuladas somam as visibilidades.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>200 com os reembolsos visíveis (lista vazia quando não há nenhum); 403 sem nenhuma role de leitura.</returns>
    [HttpGet]
    [Authorize(Roles = AppRoles.ExpenseReaders)]
    [ProducesResponseType<IReadOnlyList<ExpenseResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyList<ExpenseResponse>> result = await _expenseService.ListAsync(User.ToActor(), cancellationToken);
        return this.ToActionResult(result, expenses => Ok(expenses));
    }

    /// <summary>
    /// Consulta um reembolso visível ao usuário. Reembolso inexistente ou fora do escopo de leitura responde 404, sem distinção.
    /// </summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>200 com o reembolso; 403 sem nenhuma role de leitura; 404 para reembolso inexistente ou invisível.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.ExpenseReaders)]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        ServiceResult<ExpenseResponse> result = await _expenseService.GetAsync(User.ToActor(), id, cancellationToken);
        return this.ToActionResult(result, expense => Ok(expense));
    }
}
