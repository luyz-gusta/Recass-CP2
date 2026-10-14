using System;
using ExpenseHub.Api.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Domain;

/// <summary>
/// Histórico e pagamento sempre registram o ator que executou a ação.
/// </summary>
[TestClass]
public sealed class DomainRecordTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Histórico sem ator é rejeitado.</summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Record_BlankActor_Throws(string actorId)
    {
        Assert.ThrowsExactly<ArgumentException>(() => ExpenseHistory.Record(
            Guid.NewGuid(), ExpenseAction.Created, actorId, _now, null, ExpenseStatus.Draft));
    }

    /// <summary>A entrada de histórico guarda transição, ator, horário e justificativa informados.</summary>
    [TestMethod]
    public void Record_Rejection_KeepsTransitionActorAndJustification()
    {
        Guid expenseId = Guid.NewGuid();

        ExpenseHistory entry = ExpenseHistory.Record(
            expenseId, ExpenseAction.Rejected, "approver-1", _now, ExpenseStatus.Submitted, ExpenseStatus.Rejected, "Nota fiscal ilegível.");

        Assert.AreEqual(expenseId, entry.ExpenseId);
        Assert.AreEqual(ExpenseStatus.Submitted, entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Rejected, entry.NewStatus);
        Assert.AreEqual("approver-1", entry.ActorId);
        Assert.AreEqual(_now, entry.OccurredAtUtc);
        Assert.AreEqual("Nota fiscal ilegível.", entry.Justification);
    }

    /// <summary>Pagamento sem o usuário do financeiro é rejeitado.</summary>
    [TestMethod]
    public void CreatePayment_BlankPaidBy_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => PaymentRecord.Create(Guid.NewGuid(), 10m, " ", _now));
    }

    /// <summary>Cada pagamento recebe um identificador gerado pelo servidor.</summary>
    [TestMethod]
    public void CreatePayment_ValidData_GeneratesIdentifier()
    {
        PaymentRecord first = PaymentRecord.Create(Guid.NewGuid(), 10m, "finance-1", _now);
        PaymentRecord second = PaymentRecord.Create(Guid.NewGuid(), 10m, "finance-1", _now);

        Assert.AreNotEqual(Guid.Empty, first.Id);
        Assert.AreNotEqual(first.Id, second.Id);
    }
}
