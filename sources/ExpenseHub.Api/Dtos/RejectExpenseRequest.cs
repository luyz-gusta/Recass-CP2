using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Dtos;

/// <summary>
/// Dados para <c>POST /api/expenses/{id}/reject</c>. Aprovador, estado e horário da decisão não fazem parte do contrato:
/// vêm do usuário autenticado e do servidor, e qualquer campo extra é rejeitado com 400.
/// </summary>
public sealed class RejectExpenseRequest
{
    /// <summary>
    /// Motivo da reprovação, com 10 a 500 caracteres. É gravado no reembolso e no histórico sem os espaços nas pontas,
    /// e o serviço confere o tamanho de novo depois de removê-los.
    /// </summary>
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Justification { get; init; } = string.Empty;
}
