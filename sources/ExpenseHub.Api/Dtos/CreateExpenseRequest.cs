using System;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Dtos;

/// <summary>
/// Dados para <c>POST /api/expenses</c>. Proprietário, estado e horários não fazem parte do contrato:
/// vêm do usuário autenticado e do servidor, e qualquer campo extra é rejeitado com 400.
/// </summary>
public sealed class CreateExpenseRequest
{
    /// <summary>Descrição da despesa, com 10 a 500 caracteres.</summary>
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string Description { get; init; } = string.Empty;

    /// <summary>Valor entre 0,01 e 2.147.483.647. O tipo anulável evita que um campo ausente vire 0 em silêncio.</summary>
    [Required]
    [Range(typeof(decimal), "0.01", "2147483647", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? Amount { get; init; }

    /// <summary>Data da despesa (<c>yyyy-MM-dd</c>), que não pode ser futura.</summary>
    [Required]
    public DateOnly? ExpenseDate { get; init; }

    /// <summary>Identificador de uma categoria existente (1 a 5: Alimentação, Transporte, Hospedagem, Material de escritório e Outros).</summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int? CategoryId { get; init; }
}
