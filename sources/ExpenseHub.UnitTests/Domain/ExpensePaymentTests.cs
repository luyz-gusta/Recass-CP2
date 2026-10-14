using System;
using ExpenseHub.Api.Domain;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Domain;

/// <summary>
/// Pagamento no domínio: somente Approved vira Paid, uma única vez, com um registro de pagamento que guarda
/// valor, quem pagou e quando, todos definidos pelo servidor.
/// </summary>
[TestClass]
public sealed class ExpensePaymentTests
{
    private const string OwnerId = "employee-1";
    private const string FinanceUserId = "finance-1";

    private static readonly DateTime _createdAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _paidAt = new(2026, 10, 5, 16, 45, 0, DateTimeKind.Utc);

    /// <summary>Um reembolso aprovado pago passa para Paid.</summary>
    [TestMethod]
    public void MarkAsPaid_Approved_BecomesPaid()
    {
        Expense expense = ApprovedExpense();

        expense.MarkAsPaid(FinanceUserId, _paidAt);

        Assert.AreEqual(ExpenseStatus.Paid, expense.Status);
    }

    /// <summary>O registro de pagamento guarda o reembolso, o valor dele, quem pagou e quando, e fica associado ao reembolso.</summary>
    [TestMethod]
    public void MarkAsPaid_Approved_CreatesAssociatedPaymentRecord()
    {
        Expense expense = ApprovedExpense();

        PaymentRecord payment = expense.MarkAsPaid(FinanceUserId, _paidAt);

        Assert.AreSame(payment, expense.Payment);
        Assert.AreEqual(expense.Id, payment.ExpenseId);
        Assert.AreEqual(TestExpenses.ValidAmount, payment.Amount);
        Assert.AreEqual(FinanceUserId, payment.PaidById);
        Assert.AreEqual(_paidAt, payment.PaidAtUtc);
    }

    /// <summary>O pagamento atualiza o horário da última alteração e renova o carimbo de concorrência.</summary>
    [TestMethod]
    public void MarkAsPaid_Approved_TouchesUpdatedAtAndRenewsStamp()
    {
        Expense expense = ApprovedExpense();
        Guid previousStamp = expense.ConcurrencyStamp;

        expense.MarkAsPaid(FinanceUserId, _paidAt);

        Assert.AreEqual(_paidAt, expense.UpdatedAtUtc);
        Assert.AreNotEqual(previousStamp, expense.ConcurrencyStamp);
    }

    /// <summary>Paid é final: pagar de novo lança exceção e preserva o primeiro pagamento.</summary>
    [TestMethod]
    public void MarkAsPaid_AlreadyPaid_ThrowsAndKeepsFirstPayment()
    {
        Expense expense = ApprovedExpense();
        PaymentRecord first = expense.MarkAsPaid(FinanceUserId, _paidAt);

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.MarkAsPaid("finance-2", _paidAt.AddHours(1)));

        Assert.AreSame(first, expense.Payment);
        Assert.AreEqual(FinanceUserId, expense.Payment!.PaidById);
    }

    /// <summary>Somente Approved pode ser pago: nos demais estados o domínio lança exceção e não cria pagamento.</summary>
    [TestMethod]
    [DataRow(ExpenseStatus.Draft)]
    [DataRow(ExpenseStatus.Submitted)]
    [DataRow(ExpenseStatus.Rejected)]
    [DataRow(ExpenseStatus.Paid)]
    public void MarkAsPaid_NotApproved_ThrowsWithoutNewPayment(ExpenseStatus status)
    {
        Expense expense = ExpenseScenario.Of(OwnerId).At(_createdAt).InStatus(status);
        PaymentRecord? previousPayment = expense.Payment;

        Assert.ThrowsExactly<InvalidOperationException>(() => expense.MarkAsPaid("finance-2", _paidAt));

        Assert.AreEqual(status, expense.Status);
        Assert.AreSame(previousPayment, expense.Payment);
    }

    /// <summary>Quem paga é obrigatório: sem identificador não há pagamento.</summary>
    [TestMethod]
    public void MarkAsPaid_BlankFinanceUserId_ThrowsAndKeepsApproved()
    {
        Expense expense = ApprovedExpense();

        Assert.ThrowsExactly<ArgumentException>(() => expense.MarkAsPaid(" ", _paidAt));

        Assert.AreEqual(ExpenseStatus.Approved, expense.Status);
        Assert.IsNull(expense.Payment);
    }

    private static Expense ApprovedExpense()
    {
        return ExpenseScenario.Of(OwnerId).At(_createdAt).Approved();
    }
}
