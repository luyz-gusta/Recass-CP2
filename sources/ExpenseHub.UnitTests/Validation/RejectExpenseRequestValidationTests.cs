using System.Text.Json;
using ExpenseHub.Api.Dtos;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Validation;

/// <summary>
/// O DTO de reprovação exige justificativa de 10 a 500 caracteres e não aceita aprovador, estado nem horário vindos do cliente.
/// </summary>
[TestClass]
public sealed class RejectExpenseRequestValidationTests
{
    /// <summary>Justificativa com 10 e com 500 caracteres é aceita.</summary>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public void Validate_JustificationOnTheLimit_IsValid(int length)
    {
        RejectExpenseRequest request = new() { Justification = new string('j', length) };

        Assert.IsEmpty(DtoValidator.Validate(request));
    }

    /// <summary>Justificativa com 9 ou com 501 caracteres é rejeitada no campo <c>Justification</c>.</summary>
    [TestMethod]
    [DataRow(9)]
    [DataRow(501)]
    public void Validate_JustificationOutsideLimits_FailsOnJustification(int length)
    {
        RejectExpenseRequest request = new() { Justification = new string('j', length) };

        Assert.Contains(nameof(RejectExpenseRequest.Justification), DtoValidator.InvalidMembers(request));
    }

    /// <summary>Justificativa nula, vazia ou só com espaços é rejeitada no campo <c>Justification</c>.</summary>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("            ")]
    public void Validate_JustificationBlank_FailsOnJustification(string? justification)
    {
        RejectExpenseRequest request = new() { Justification = justification! };

        Assert.Contains(nameof(RejectExpenseRequest.Justification), DtoValidator.InvalidMembers(request));
    }

    /// <summary>Justificativa ausente do corpo não vira texto vazio aceito: o DTO fica inválido.</summary>
    [TestMethod]
    public void Deserialize_EmptyBody_FailsOnJustification()
    {
        RejectExpenseRequest? request = JsonSerializer.Deserialize<RejectExpenseRequest>("{}", ApiJson.Options);

        Assert.IsNotNull(request);
        Assert.Contains(nameof(RejectExpenseRequest.Justification), DtoValidator.InvalidMembers(request));
    }

    /// <summary>Aprovador, estado e horários enviados no corpo são rejeitados: ator e horário vêm do servidor.</summary>
    [TestMethod]
    [DataRow("decidedById")]
    [DataRow("decidedAtUtc")]
    [DataRow("status")]
    [DataRow("rejectionReason")]
    [DataRow("ownerId")]
    public void Deserialize_ServerControlledField_IsRejected(string field)
    {
        string json = $$"""
            { "justification": "Comprovante ilegível, envie novamente.", "{{field}}": "valor-do-cliente" }
            """;

        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<RejectExpenseRequest>(json, ApiJson.Options));
    }
}
