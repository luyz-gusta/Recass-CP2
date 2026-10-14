using System;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Contracts;

/// <summary>
/// Representação pública de um reembolso. Datas e horas saem em UTC.
/// </summary>
/// <param name="Id">Identificador gerado pelo servidor.</param>
/// <param name="OwnerId">Identificador do usuário proprietário, obtido do token na criação.</param>
/// <param name="CategoryId">Identificador da categoria.</param>
/// <param name="CategoryName">Nome da categoria.</param>
/// <param name="Description">Descrição da despesa.</param>
/// <param name="Amount">Valor do reembolso.</param>
/// <param name="ExpenseDate">Data da despesa.</param>
/// <param name="Status">Estado atual, definido somente pelo servidor.</param>
/// <param name="CreatedAtUtc">Instante da criação.</param>
/// <param name="UpdatedAtUtc">Instante da última alteração.</param>
/// <param name="SubmittedAtUtc">Instante do envio, quando já enviado.</param>
/// <param name="DecidedById">Quem aprovou ou reprovou, quando já decidido.</param>
/// <param name="DecidedAtUtc">Instante da decisão, quando já decidido.</param>
/// <param name="RejectionReason">Justificativa da reprovação, quando reprovado.</param>
/// <param name="PaidById">Quem registrou o pagamento, quando pago.</param>
/// <param name="PaidAtUtc">Instante do pagamento, quando pago.</param>
public sealed record ExpenseResponse(
    Guid Id,
    string OwnerId,
    int CategoryId,
    string CategoryName,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate,
    ExpenseStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? SubmittedAtUtc,
    string? DecidedById,
    DateTime? DecidedAtUtc,
    string? RejectionReason,
    string? PaidById,
    DateTime? PaidAtUtc);
