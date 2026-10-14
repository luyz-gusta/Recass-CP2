using System;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Dados editáveis de um reembolso já validados, comuns à criação e à edição de rascunhos.
/// </summary>
/// <param name="CategoryId">Categoria existente.</param>
/// <param name="Description">Descrição informada.</param>
/// <param name="Amount">Valor informado.</param>
/// <param name="ExpenseDate">Data da despesa, nunca futura.</param>
internal sealed record ExpenseInput(int CategoryId, string Description, decimal Amount, DateOnly ExpenseDate);
