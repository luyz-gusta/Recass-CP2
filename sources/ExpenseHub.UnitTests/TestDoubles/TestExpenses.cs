using System;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.UnitTests.TestDoubles;

/// <summary>
/// Reembolsos prontos para o arrange dos testes. Os dados padrão são válidos para o contrato de validação.
/// </summary>
internal static class TestExpenses
{
    public const string ValidDescription = "Almoço com cliente em São Paulo";

    public const decimal ValidAmount = 150.50m;

    public const int ValidCategoryId = 1;

    public static DateOnly ValidDate { get; } = new(2026, 9, 30);

    /// <summary>Cria um rascunho válido do proprietário informado, criado em <paramref name="createdAtUtc"/> (padrão: 01/10/2026 12:00 UTC).</summary>
    public static Expense Draft(string ownerId, DateTime? createdAtUtc = null)
    {
        DateTime now = createdAtUtc ?? new FixedTimeProvider().UtcNow;
        return Expense.CreateDraft(ownerId, ValidCategoryId, ValidDescription, ValidAmount, ValidDate, now);
    }

    /// <summary>
    /// Força o estado de um reembolso no arrange, sem passar pelas transições do domínio.
    /// Serve para montar situações que só existem depois de várias etapas (por exemplo, um reembolso já aprovado).
    /// </summary>
    public static Expense InStatus(Expense expense, ExpenseStatus status)
    {
        ArgumentNullException.ThrowIfNull(expense);

        typeof(Expense).GetProperty(nameof(Expense.Status))!.SetValue(expense, status);
        return expense;
    }
}
