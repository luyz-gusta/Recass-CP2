using System;
using ExpenseHub.Api.Domain;

namespace ExpenseHub.Api.Contracts;

/// <summary>
/// Uma entrada do histórico de um reembolso. Ator e horário (UTC) são definidos pelo servidor.
/// </summary>
/// <param name="Id">Identificador da entrada; acompanha a ordem de gravação.</param>
/// <param name="Action">Ação executada (criação, edição, envio, aprovação, reprovação ou pagamento).</param>
/// <param name="ActorId">Identificador do usuário que executou a ação.</param>
/// <param name="OccurredAtUtc">Instante da ação, em UTC.</param>
/// <param name="PreviousStatus">Estado anterior; nulo na criação.</param>
/// <param name="NewStatus">Estado depois da ação.</param>
/// <param name="Justification">Justificativa, somente na reprovação.</param>
/// <param name="Changes">Alterações feitas no rascunho, no formato <c>campo: antigo -> novo; ...</c>, somente na edição.</param>
public sealed record ExpenseHistoryResponse(
    long Id,
    ExpenseAction Action,
    string ActorId,
    DateTime OccurredAtUtc,
    ExpenseStatus? PreviousStatus,
    ExpenseStatus NewStatus,
    string? Justification,
    string? Changes);
