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
/// Fluxo dos reembolsos depois do envio (aprovar, reprovar e pagar) e consulta do histórico. A role é verificada aqui;
/// escopo, ownership e estado são decididos na camada de serviço. Ator e horário nunca vêm do cliente.
/// </summary>
[ApiController]
[Route("api/expenses")]
public sealed class ExpenseWorkflowController : ControllerBase
{
    private readonly IExpenseWorkflowService _workflowService;

    /// <summary>
    /// Cria o controller do fluxo de aprovação.
    /// </summary>
    /// <param name="workflowService">Serviço de decisões sobre reembolsos.</param>
    public ExpenseWorkflowController(IExpenseWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    /// <summary>
    /// Aprova um reembolso enviado de outra pessoa (Submitted para Approved). Aprovar o próprio reembolso responde 403,
    /// mesmo acumulando roles; repetir a decisão responde 409.
    /// </summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// 200 com o reembolso aprovado; 403 sem a role Approver ou no próprio reembolso; 404 para reembolso inexistente
    /// ou rascunho de outra pessoa; 409 fora de Submitted.
    /// </returns>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = AppRoles.Approver)]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        ServiceResult<ExpenseResponse> result = await _workflowService.ApproveAsync(User.ToActor(), id, cancellationToken);
        return this.ToActionResult(result, expense => Ok(expense));
    }

    /// <summary>
    /// Reprova um reembolso enviado de outra pessoa (Submitted para Rejected) com justificativa de 10 a 500 caracteres.
    /// Reprovar o próprio reembolso responde 403, mesmo acumulando roles; repetir a decisão responde 409.
    /// </summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="request">Justificativa da reprovação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// 200 com o reembolso reprovado; 400 para justificativa ausente ou fora do tamanho; 403 sem a role Approver ou no
    /// próprio reembolso; 404 para reembolso inexistente ou rascunho de outra pessoa; 409 fora de Submitted.
    /// </returns>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = AppRoles.Approver)]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectAsync(Guid id, RejectExpenseRequest request, CancellationToken cancellationToken)
    {
        ServiceResult<ExpenseResponse> result = await _workflowService.RejectAsync(User.ToActor(), id, request, cancellationToken);
        return this.ToActionResult(result, expense => Ok(expense));
    }

    /// <summary>
    /// Registra o pagamento de um reembolso aprovado de outra pessoa (Approved para Paid). Pagar o próprio reembolso
    /// responde 403, mesmo acumulando roles; um segundo pagamento responde 409.
    /// </summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>
    /// 200 com o reembolso pago (<c>paidById</c> e <c>paidAtUtc</c> preenchidos pelo servidor); 403 sem a role Finance ou no
    /// próprio reembolso; 404 para reembolso inexistente ou rascunho de outra pessoa; 409 fora de Approved.
    /// </returns>
    [HttpPost("{id:guid}/pay")]
    [Authorize(Roles = AppRoles.Finance)]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PayAsync(Guid id, CancellationToken cancellationToken)
    {
        ServiceResult<ExpenseResponse> result = await _workflowService.PayAsync(User.ToActor(), id, cancellationToken);
        return this.ToActionResult(result, expense => Ok(expense));
    }

    /// <summary>
    /// Consulta o histórico de um reembolso em ordem cronológica, com a mesma visibilidade do detalhe.
    /// Reembolso inexistente ou fora do escopo de leitura responde 404, sem distinção.
    /// </summary>
    /// <param name="id">Identificador do reembolso.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>200 com as entradas do histórico; 403 sem nenhuma role de leitura; 404 para reembolso inexistente ou invisível.</returns>
    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = AppRoles.ExpenseReaders)]
    [ProducesResponseType<IReadOnlyList<ExpenseHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyList<ExpenseHistoryResponse>> result = await _workflowService.GetHistoryAsync(User.ToActor(), id, cancellationToken);
        return this.ToActionResult(result, entries => Ok(entries));
    }
}
