using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// A resposta pública reflete o reembolso e tolera dados ainda não carregados.
/// </summary>
[TestClass]
public sealed class ExpenseMapperTests
{
    /// <summary>Todos os campos do rascunho aparecem na resposta, e os dados de etapas futuras ficam nulos.</summary>
    [TestMethod]
    public void ToResponse_Draft_CopiesFieldsAndLeavesLaterStepsNull()
    {
        Expense expense = TestExpenses.Draft("employee-1");

        ExpenseResponse response = expense.ToResponse();

        Assert.AreEqual(expense.Id, response.Id);
        Assert.AreEqual("employee-1", response.OwnerId);
        Assert.AreEqual(TestExpenses.ValidCategoryId, response.CategoryId);
        Assert.AreEqual(TestExpenses.ValidDescription, response.Description);
        Assert.AreEqual(TestExpenses.ValidAmount, response.Amount);
        Assert.AreEqual(TestExpenses.ValidDate, response.ExpenseDate);
        Assert.AreEqual(ExpenseStatus.Draft, response.Status);
        Assert.AreEqual(expense.CreatedAtUtc, response.CreatedAtUtc);
        Assert.AreEqual(expense.UpdatedAtUtc, response.UpdatedAtUtc);
        Assert.IsNull(response.SubmittedAtUtc);
        Assert.IsNull(response.DecidedById);
        Assert.IsNull(response.DecidedAtUtc);
        Assert.IsNull(response.RejectionReason);
        Assert.IsNull(response.PaidById);
        Assert.IsNull(response.PaidAtUtc);
    }

    /// <summary>Sem a navegação da categoria carregada, o nome sai vazio em vez de lançar exceção.</summary>
    [TestMethod]
    public void ToResponse_CategoryNotLoaded_ReturnsEmptyCategoryName()
    {
        Expense expense = TestExpenses.Draft("employee-1");

        ExpenseResponse response = expense.ToResponse();

        Assert.AreEqual(string.Empty, response.CategoryName);
    }
}
