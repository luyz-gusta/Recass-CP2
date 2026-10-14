namespace ExpenseHub.Api.Domain;

/// <summary>
/// Estados de um reembolso. O estado é definido exclusivamente pelo servidor.
/// </summary>
public enum ExpenseStatus
{
    /// <summary>Rascunho, editável somente pelo proprietário.</summary>
    Draft = 0,

    /// <summary>Enviado para aprovação.</summary>
    Submitted = 1,

    /// <summary>Aprovado por um Approver que não é o proprietário.</summary>
    Approved = 2,

    /// <summary>Reprovado com justificativa. Estado final.</summary>
    Rejected = 3,

    /// <summary>Pagamento registrado pelo financeiro. Estado final.</summary>
    Paid = 4,
}
