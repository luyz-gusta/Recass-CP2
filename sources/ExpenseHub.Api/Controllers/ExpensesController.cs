using System;
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
}
