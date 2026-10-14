namespace ExpenseHub.Api.Domain;

/// <summary>
/// Ações registradas no histórico de um reembolso.
/// </summary>
public enum ExpenseAction
{
    /// <summary>Rascunho criado.</summary>
    Created = 0,

    /// <summary>Rascunho alterado pelo proprietário.</summary>
    Updated = 1,

    /// <summary>Rascunho enviado para aprovação.</summary>
    Submitted = 2,

    /// <summary>Reembolso aprovado.</summary>
    Approved = 3,

    /// <summary>Reembolso reprovado com justificativa.</summary>
    Rejected = 4,

    /// <summary>Pagamento registrado.</summary>
    Paid = 5,
}
