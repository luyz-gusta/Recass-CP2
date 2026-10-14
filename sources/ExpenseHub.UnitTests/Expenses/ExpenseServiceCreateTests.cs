using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Services;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Criação de rascunhos: role Employee, proprietário vindo do ator, validações de serviço e gravação única com histórico.
/// </summary>
[TestClass]
public sealed class ExpenseServiceCreateTests
{
    private readonly InMemoryExpenseRepository _repository = new();
    private readonly FixedTimeProvider _clock = new();
    private readonly ExpenseService _service;

    /// <summary>Monta o serviço com o repositório em memória e o relógio fixo.</summary>
    public ExpenseServiceCreateTests()
    {
        _service = new ExpenseService(_repository, _clock);
    }

    /// <summary>O rascunho criado fica em Draft e pertence ao ator autenticado.</summary>
    [TestMethod]
    public async Task CreateAsync_EmployeeWithValidData_CreatesDraftOwnedByActor()
    {
        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.Employee1, ValidRequest(), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(ExpenseStatus.Draft, result.Value!.Status);
        Assert.AreEqual(TestActors.Employee1.UserId, result.Value.OwnerId);
        Expense stored = Assert.ContainsSingle(_repository.Expenses);
        Assert.AreEqual(result.Value.Id, stored.Id);
        Assert.AreNotEqual(Guid.Empty, stored.Id);
    }

    /// <summary>A resposta devolve os dados informados e nenhuma etapa posterior preenchida.</summary>
    [TestMethod]
    public async Task CreateAsync_EmployeeWithValidData_ReturnsRequestedData()
    {
        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.Employee1, ValidRequest(), CancellationToken.None);

        ExpenseResponse response = result.Value!;
        Assert.AreEqual(TestExpenses.ValidCategoryId, response.CategoryId);
        Assert.AreEqual(TestExpenses.ValidDescription, response.Description);
        Assert.AreEqual(TestExpenses.ValidAmount, response.Amount);
        Assert.AreEqual(TestExpenses.ValidDate, response.ExpenseDate);
        Assert.IsNull(response.SubmittedAtUtc);
        Assert.IsNull(response.DecidedById);
        Assert.IsNull(response.RejectionReason);
        Assert.IsNull(response.PaidById);
    }

    /// <summary>Os horários vêm do relógio do servidor.</summary>
    [TestMethod]
    public async Task CreateAsync_EmployeeWithValidData_UsesServerClock()
    {
        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.Employee1, ValidRequest(), CancellationToken.None);

        Assert.AreEqual(_clock.UtcNow, result.Value!.CreatedAtUtc);
        Assert.AreEqual(_clock.UtcNow, result.Value.UpdatedAtUtc);
    }

    /// <summary>O reembolso e o histórico <c>Created</c> são gravados juntos, num único commit.</summary>
    [TestMethod]
    public async Task CreateAsync_EmployeeWithValidData_RecordsCreatedHistoryInSingleSave()
    {
        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.Employee1, ValidRequest(), CancellationToken.None);

        ExpenseHistory entry = Assert.ContainsSingle(_repository.History);
        Assert.AreEqual(result.Value!.Id, entry.ExpenseId);
        Assert.AreEqual(ExpenseAction.Created, entry.Action);
        Assert.AreEqual(TestActors.Employee1.UserId, entry.ActorId);
        Assert.AreEqual(_clock.UtcNow, entry.OccurredAtUtc);
        Assert.IsNull(entry.PreviousStatus);
        Assert.AreEqual(ExpenseStatus.Draft, entry.NewStatus);
        Assert.AreEqual(1, _repository.SaveChangesCount);
    }

    /// <summary>Quem acumula Employee com outra role cria o próprio rascunho.</summary>
    [TestMethod]
    public async Task CreateAsync_ApproverWithEmployeeRole_CreatesOwnDraft()
    {
        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.ApproverEmployee, ValidRequest(), CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(TestActors.ApproverEmployee.UserId, result.Value!.OwnerId);
    }

    /// <summary>Roles sem Employee não criam reembolso (403) e nada é gravado.</summary>
    [TestMethod]
    [DataRow(AppRoles.Approver)]
    [DataRow(AppRoles.Finance)]
    [DataRow(AppRoles.Auditor)]
    [DataRow(AppRoles.Admin)]
    public async Task CreateAsync_ActorWithoutEmployeeRole_ReturnsForbiddenAndSavesNothing(string role)
    {
        Actor actor = TestActors.Create("user-without-employee", role);

        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(actor, ValidRequest(), CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Usuário sem nenhuma role não cria reembolso (403).</summary>
    [TestMethod]
    public async Task CreateAsync_ActorWithNoRole_ReturnsForbiddenAndSavesNothing()
    {
        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.NoRole, ValidRequest(), CancellationToken.None);

        Assert.AreEqual(ServiceError.Forbidden, result.Error);
        AssertNothingSaved();
    }

    /// <summary>Data futura é rejeitada no campo <c>ExpenseDate</c>.</summary>
    [TestMethod]
    public async Task CreateAsync_FutureDate_ReturnsValidationOnExpenseDate()
    {
        CreateExpenseRequest request = RequestWith(expenseDate: _clock.Today.AddDays(1));

        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.Employee1, request, CancellationToken.None);

        Assert.AreEqual(ServiceError.Validation, result.Error);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(CreateExpenseRequest.ExpenseDate)));
        AssertNothingSaved();
    }

    /// <summary>A data de hoje é aceita.</summary>
    [TestMethod]
    public async Task CreateAsync_TodayDate_Succeeds()
    {
        CreateExpenseRequest request = RequestWith(expenseDate: _clock.Today);

        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.Employee1, request, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(_clock.Today, result.Value!.ExpenseDate);
    }

    /// <summary>Categoria inexistente é rejeitada no campo <c>CategoryId</c>.</summary>
    [TestMethod]
    [DataRow(6)]
    [DataRow(99)]
    public async Task CreateAsync_UnknownCategory_ReturnsValidationOnCategoryId(int categoryId)
    {
        CreateExpenseRequest request = RequestWith(categoryId: categoryId);

        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.Employee1, request, CancellationToken.None);

        Assert.AreEqual(ServiceError.Validation, result.Error);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(CreateExpenseRequest.CategoryId)));
        AssertNothingSaved();
    }

    /// <summary>Os erros de campos diferentes voltam juntos, para o cliente corrigir tudo de uma vez.</summary>
    [TestMethod]
    public async Task CreateAsync_FutureDateAndUnknownCategory_ReturnsBothFieldErrors()
    {
        CreateExpenseRequest request = RequestWith(expenseDate: _clock.Today.AddDays(30), categoryId: 99);

        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.Employee1, request, CancellationToken.None);

        Assert.HasCount(2, result.ValidationErrors);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(CreateExpenseRequest.ExpenseDate)));
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(CreateExpenseRequest.CategoryId)));
    }

    /// <summary>Mesmo sem o controller, campos ausentes viram erro de validação e não exceção.</summary>
    [TestMethod]
    public async Task CreateAsync_MissingFields_ReturnsValidationForEachField()
    {
        CreateExpenseRequest request = new();

        ServiceResult<ExpenseResponse> result = await _service.CreateAsync(TestActors.Employee1, request, CancellationToken.None);

        Assert.AreEqual(ServiceError.Validation, result.Error);
        Assert.HasCount(4, result.ValidationErrors);
        AssertNothingSaved();
    }

    /// <summary>Uma falha de persistência não deixa reembolso nem histórico pela metade.</summary>
    [TestMethod]
    public async Task CreateAsync_SaveFails_PersistsNeitherExpenseNorHistory()
    {
        _repository.FailNextSave = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => _service.CreateAsync(TestActors.Employee1, ValidRequest(), CancellationToken.None));

        Assert.IsEmpty(_repository.Expenses);
        Assert.IsEmpty(_repository.History);
    }

    private static CreateExpenseRequest ValidRequest()
    {
        return RequestWith();
    }

    private static CreateExpenseRequest RequestWith(DateOnly? expenseDate = null, int categoryId = TestExpenses.ValidCategoryId)
    {
        return new CreateExpenseRequest
        {
            Description = TestExpenses.ValidDescription,
            Amount = TestExpenses.ValidAmount,
            ExpenseDate = expenseDate ?? TestExpenses.ValidDate,
            CategoryId = categoryId,
        };
    }

    private void AssertNothingSaved()
    {
        Assert.IsEmpty(_repository.Expenses);
        Assert.IsEmpty(_repository.History);
        Assert.AreEqual(0, _repository.SaveChangesCount);
    }
}
