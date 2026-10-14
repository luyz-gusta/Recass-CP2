using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Domain;

/// <summary>
/// Aprovação e reprovação no domínio: somente Submitted recebe decisão, uma única vez, com aprovador, horário
/// e justificativa (na reprovação) definidos pelo servidor.
/// </summary>
[TestClass]
public sealed class ExpenseDecisionTests
{
    private const string OwnerId = "employee-1";
    private const string ApproverId = "approver-1";
    private const string ValidJustification = "Comprovante ilegível, envie novamente.";

    private static readonly DateTime _createdAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _submittedAt = new(2026, 10, 2, 8, 15, 0, DateTimeKind.Utc);
    private static readonly DateTime _decidedAt = new(2026, 10, 3, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>Um reembolso enviado aprovado passa para Approved e registra quem decidiu e quando.</summary>
    [TestMethod]
    public void Approve_Submitted_BecomesApprovedWithDeciderAndTime()
    {
        Expense expense = SubmittedExpense();

        expense.Approve(ApproverId, _decidedAt);

        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        Assert.AreEqual(ApproverId, expense.DecidedById);
        Assert.AreEqual(_decidedAt, expense.DecidedAtUtc);
    }

    /// <summary>A aprovação atualiza o horário da última alteração e renova o carimbo de concorrência.</summary>
    [TestMethod]
    public void Approve_Submitted_TouchesUpdatedAtAndRenewsStamp()
    {
        Expense expense = SubmittedExpense();
        Guid previousStamp = expense.ConcurrencyStamp;

        expense.Approve(ApproverId, _decidedAt);

        Assert.AreEqual(_decidedAt, expense.UpdatedAtUtc);
        Assert.AreNotEqual(previousStamp, expense.ConcurrencyStamp);
    }

    /// <summary>A decisão é única: aprovar de novo lança exceção e preserva a primeira decisão.</summary>
    [TestMethod]
    public void Approve_AlreadyApproved_ThrowsAndKeepsFirstDecision()
    {
        Expense expense = SubmittedExpense();
        expense.Approve(ApproverId, _decidedAt);

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.Approve("approver-2", _decidedAt.AddHours(1)));

        Assert.AreEqual(ApproverId, expense.DecidedById);
        Assert.AreEqual(_decidedAt, expense.DecidedAtUtc);
    }

    /// <summary>Somente Submitted pode ser aprovado: nos demais estados o domínio lança exceção (defesa em profundidade).</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public void Approve_NotSubmitted_ThrowsAndKeepsStatus(ExpenseStatus status)
    {
        Expense expense = ExpenseScenario.Of(OwnerId).At(_createdAt).InStatus(status);
        string? previousDecider = expense.DecidedById;

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.Approve("approver-2", _decidedAt));

        Assert.AreEqual(status, expense.Status);
        Assert.AreEqual(previousDecider, expense.DecidedById);
    }

    /// <summary>O aprovador é obrigatório: sem identificador não há decisão.</summary>
    [TestMethod]
    public void Approve_BlankApproverId_ThrowsAndKeepsSubmitted()
    {
        Expense expense = SubmittedExpense();

        Assert.ThrowsExactly<ArgumentException>(() => expense.Approve(" ", _decidedAt));

        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
    }

    /// <summary>Um reembolso enviado reprovado passa para Rejected e registra quem decidiu, quando e por quê.</summary>
    [TestMethod]
    public void Reject_Submitted_BecomesRejectedWithDeciderTimeAndReason()
    {
        Expense expense = SubmittedExpense();

        expense.Reject(ApproverId, ValidJustification, _decidedAt);

        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual(ApproverId, expense.DecidedById);
        Assert.AreEqual(_decidedAt, expense.DecidedAtUtc);
        Assert.AreEqual(ValidJustification, expense.RejectionReason);
    }

    /// <summary>A reprovação atualiza o horário da última alteração e renova o carimbo de concorrência.</summary>
    [TestMethod]
    public void Reject_Submitted_TouchesUpdatedAtAndRenewsStamp()
    {
        Expense expense = SubmittedExpense();
        Guid previousStamp = expense.ConcurrencyStamp;

        expense.Reject(ApproverId, ValidJustification, _decidedAt);

        Assert.AreEqual(_decidedAt, expense.UpdatedAtUtc);
        Assert.AreNotEqual(previousStamp, expense.ConcurrencyStamp);
    }

    /// <summary>A justificativa é gravada sem os espaços nas pontas.</summary>
    [TestMethod]
    public void Reject_JustificationWithSurroundingSpaces_StoresTrimmedReason()
    {
        Expense expense = SubmittedExpense();

        expense.Reject(ApproverId, "   " + ValidJustification + "  ", _decidedAt);

        Assert.AreEqual(ValidJustification, expense.RejectionReason);
    }

    /// <summary>
    /// Os limites de 10 e de 500 caracteres são aceitos. Os valores são os do contrato, escritos aqui, e não as constantes
    /// do domínio: se alguém mudar a constante, o teste falha.
    /// </summary>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public void Reject_JustificationAtLengthLimit_IsAccepted(int length)
    {
        Expense expense = SubmittedExpense();
        string justification = new('j', length);

        expense.Reject(ApproverId, justification, _decidedAt);

        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual(justification, expense.RejectionReason);
    }

    /// <summary>Justificativa vazia, com 9 ou com 501 caracteres é recusada e o reembolso continua Submitted (limites do contrato, literais).</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(9)]
    [DataRow(501)]
    public void Reject_JustificationOutOfLength_ThrowsAndKeepsSubmitted(int length)
    {
        Expense expense = SubmittedExpense();

        Assert.ThrowsExactly<ArgumentException>(() => expense.Reject(ApproverId, new string('j', length), _decidedAt));

        AssertStillUndecided(expense);
    }

    /// <summary>Justificativa nula é recusada.</summary>
    [TestMethod]
    public void Reject_NullJustification_ThrowsAndKeepsSubmitted()
    {
        Expense expense = SubmittedExpense();

        Assert.ThrowsExactly<ArgumentException>(() => expense.Reject(ApproverId, null!, _decidedAt));

        AssertStillUndecided(expense);
    }

    /// <summary>Justificativa só com espaços é recusada: o tamanho é medido depois de remover os espaços nas pontas.</summary>
    [TestMethod]
    [DataRow("            ")]
    [DataRow("    123456789    ")]
    public void Reject_JustificationTooShortAfterTrim_ThrowsAndKeepsSubmitted(string justification)
    {
        Expense expense = SubmittedExpense();

        Assert.ThrowsExactly<ArgumentException>(() => expense.Reject(ApproverId, justification, _decidedAt));

        AssertStillUndecided(expense);
    }

    /// <summary>Somente Submitted pode ser reprovado: nos demais estados o domínio lança exceção (defesa em profundidade).</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Approved)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public void Reject_NotSubmitted_ThrowsAndKeepsStatus(ExpenseStatus status)
    {
        Expense expense = ExpenseScenario.Of(OwnerId).At(_createdAt).InStatus(status);
        string? previousReason = expense.RejectionReason;

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.Reject(ApproverId, ValidJustification, _decidedAt));

        Assert.AreEqual(status, expense.Status);
        Assert.AreEqual(previousReason, expense.RejectionReason);
    }

    /// <summary>Rejected é final: depois de reprovado, o reembolso não pode ser aprovado.</summary>
    [TestMethod]
    public void Approve_AfterRejection_ThrowsAndKeepsRejected()
    {
        Expense expense = SubmittedExpense();
        expense.Reject(ApproverId, ValidJustification, _decidedAt);

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.Approve("approver-2", _decidedAt.AddHours(1)));

        Assert.AreEqual(ExpenseStatus.Rejected, expense.Status);
        Assert.AreEqual(ValidJustification, expense.RejectionReason);
    }

    private static Expense SubmittedExpense()
    {
        return ExpenseScenario.Of(OwnerId).At(_submittedAt).Submitted();
    }

    private static void AssertStillUndecided(Expense expense)
    {
        Assert.AreEqual(ExpenseStatus.Submitted, expense.Status);
        Assert.IsNull(expense.DecidedById);
        Assert.IsNull(expense.RejectionReason);
    }
}
