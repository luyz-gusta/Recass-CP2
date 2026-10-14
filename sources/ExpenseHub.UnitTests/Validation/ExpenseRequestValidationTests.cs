using System;
using System.Globalization;
using System.Text.Json;
using ExpenseHub.Api.Dtos;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Validation;

/// <summary>
/// Os DTOs de criação e edição aplicam o mesmo contrato de validação e rejeitam campos que o cliente não pode informar.
/// Cada regra é verificada nos dois DTOs.
/// </summary>
[TestClass]
public sealed class ExpenseRequestValidationTests
{
    private static readonly DateOnly _date = new(2026, 9, 30);

    /// <summary>Uma requisição completa e dentro dos limites é válida.</summary>
    [TestMethod]
    public void Validate_CompleteRequest_IsValid()
    {
        foreach (object request in BuildBoth(TestExpenses.ValidDescription, 150.50m, _date, 1))
        {
            Assert.IsEmpty(DtoValidator.Validate(request));
        }
    }

    /// <summary>O valor mínimo (0,01) e o máximo (2147483647) são aceitos.</summary>
    [TestMethod]
    [DataRow("0.01")]
    [DataRow("2147483647")]
    public void Validate_AmountOnTheLimit_IsValid(string amount)
    {
        foreach (object request in BuildBoth(TestExpenses.ValidDescription, Parse(amount), _date, 1))
        {
            Assert.IsEmpty(DtoValidator.Validate(request));
        }
    }

    /// <summary>Zero, negativo, abaixo de 0,01 e acima de 2147483647 são rejeitados no campo <c>Amount</c>.</summary>
    [TestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("0.009")]
    [DataRow("2147483647.01")]
    public void Validate_AmountOutsideLimits_FailsOnAmount(string amount)
    {
        foreach (object request in BuildBoth(TestExpenses.ValidDescription, Parse(amount), _date, 1))
        {
            Assert.Contains(nameof(CreateExpenseRequest.Amount), DtoValidator.InvalidMembers(request));
        }
    }

    /// <summary>Valor ausente é rejeitado em vez de virar 0 em silêncio.</summary>
    [TestMethod]
    public void Validate_AmountMissing_FailsOnAmount()
    {
        foreach (object request in BuildBoth(TestExpenses.ValidDescription, null, _date, 1))
        {
            Assert.Contains(nameof(CreateExpenseRequest.Amount), DtoValidator.InvalidMembers(request));
        }
    }

    /// <summary>Os limites do valor não dependem da cultura da máquina (em pt-BR, "0.01" não pode virar 1).</summary>
    [TestMethod]
    public void Validate_AmountOnPortugueseCulture_KeepsInvariantLimits()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");

        try
        {
            foreach (object request in BuildBoth(TestExpenses.ValidDescription, 0.01m, _date, 1))
            {
                Assert.IsEmpty(DtoValidator.Validate(request));
            }

            foreach (object request in BuildBoth(TestExpenses.ValidDescription, 0.009m, _date, 1))
            {
                Assert.Contains(nameof(CreateExpenseRequest.Amount), DtoValidator.InvalidMembers(request));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Descrição com 10 e com 500 caracteres é aceita.</summary>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public void Validate_DescriptionOnTheLimit_IsValid(int length)
    {
        foreach (object request in BuildBoth(new string('a', length), 10m, _date, 1))
        {
            Assert.IsEmpty(DtoValidator.Validate(request));
        }
    }

    /// <summary>Descrição com 9 ou 501 caracteres é rejeitada no campo <c>Description</c>.</summary>
    [TestMethod]
    [DataRow(9)]
    [DataRow(501)]
    public void Validate_DescriptionOutsideLimits_FailsOnDescription(int length)
    {
        foreach (object request in BuildBoth(new string('a', length), 10m, _date, 1))
        {
            Assert.Contains(nameof(CreateExpenseRequest.Description), DtoValidator.InvalidMembers(request));
        }
    }

    /// <summary>Descrição nula, vazia ou só com espaços é rejeitada.</summary>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("          ")]
    public void Validate_DescriptionBlank_FailsOnDescription(string? description)
    {
        foreach (object request in BuildBoth(description!, 10m, _date, 1))
        {
            Assert.Contains(nameof(CreateExpenseRequest.Description), DtoValidator.InvalidMembers(request));
        }
    }

    /// <summary>Data ausente é rejeitada em vez de virar 0001-01-01 em silêncio.</summary>
    [TestMethod]
    public void Validate_ExpenseDateMissing_FailsOnExpenseDate()
    {
        foreach (object request in BuildBoth(TestExpenses.ValidDescription, 10m, null, 1))
        {
            Assert.Contains(nameof(CreateExpenseRequest.ExpenseDate), DtoValidator.InvalidMembers(request));
        }
    }

    /// <summary>Categoria ausente, zero ou negativa é rejeitada no campo <c>CategoryId</c>.</summary>
    [TestMethod]
    [DataRow(null)]
    [DataRow(0)]
    [DataRow(-1)]
    public void Validate_CategoryMissingOrNotPositive_FailsOnCategoryId(int? categoryId)
    {
        foreach (object request in BuildBoth(TestExpenses.ValidDescription, 10m, _date, categoryId))
        {
            Assert.Contains(nameof(CreateExpenseRequest.CategoryId), DtoValidator.InvalidMembers(request));
        }
    }

    /// <summary>Proprietário, estado, ator e horários enviados no corpo são rejeitados (sem mass assignment).</summary>
    [TestMethod]
    [DataRow("ownerId")]
    [DataRow("status")]
    [DataRow("id")]
    [DataRow("createdAtUtc")]
    [DataRow("updatedAtUtc")]
    [DataRow("submittedAtUtc")]
    [DataRow("decidedById")]
    [DataRow("paidById")]
    public void Deserialize_ServerControlledField_IsRejected(string field)
    {
        string json = $$"""
            { "description": "Almoço com cliente em São Paulo", "amount": 150.5, "expenseDate": "2026-09-30", "categoryId": 1, "{{field}}": "valor-do-cliente" }
            """;

        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<CreateExpenseRequest>(json, ApiJson.Options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<UpdateExpenseRequest>(json, ApiJson.Options));
    }

    /// <summary>Um corpo com somente os quatro campos do contrato é lido normalmente.</summary>
    [TestMethod]
    public void Deserialize_ContractFieldsOnly_ReadsAllValues()
    {
        const string json = """
            { "description": "Almoço com cliente em São Paulo", "amount": 150.5, "expenseDate": "2026-09-30", "categoryId": 2 }
            """;

        CreateExpenseRequest? request = JsonSerializer.Deserialize<CreateExpenseRequest>(json, ApiJson.Options);

        Assert.IsNotNull(request);
        Assert.AreEqual(TestExpenses.ValidDescription, request.Description);
        Assert.AreEqual(150.5m, request.Amount);
        Assert.AreEqual(_date, request.ExpenseDate);
        Assert.AreEqual(2, request.CategoryId);
    }

    private static decimal Parse(string value)
    {
        return decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    private static object[] BuildBoth(string description, decimal? amount, DateOnly? expenseDate, int? categoryId)
    {
        return
        [
            new CreateExpenseRequest { Description = description, Amount = amount, ExpenseDate = expenseDate, CategoryId = categoryId },
            new UpdateExpenseRequest { Description = description, Amount = amount, ExpenseDate = expenseDate, CategoryId = categoryId },
        ];
    }
}
