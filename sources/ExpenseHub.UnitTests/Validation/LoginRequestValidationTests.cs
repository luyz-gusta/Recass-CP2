using ExpenseHub.Api.Dtos;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Validation;

/// <summary>
/// O login exige e-mail válido e senha preenchida antes de consultar o Identity.
/// </summary>
[TestClass]
public sealed class LoginRequestValidationTests
{
    private static readonly string _anyCredential = new('x', 12);

    /// <summary>E-mail válido e senha preenchida passam na validação.</summary>
    [TestMethod]
    public void Validate_EmailAndCredentialFilled_IsValid()
    {
        LoginRequest request = new() { Email = "funcionario1@expensehub.local", Password = _anyCredential };

        Assert.IsEmpty(DtoValidator.Validate(request));
    }

    /// <summary>E-mail ausente ou malformado é rejeitado.</summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("nao-e-email")]
    public void Validate_InvalidEmail_FailsOnEmail(string email)
    {
        LoginRequest request = new() { Email = email, Password = _anyCredential };

        Assert.Contains(nameof(LoginRequest.Email), DtoValidator.InvalidMembers(request));
    }

    /// <summary>Senha vazia é rejeitada.</summary>
    [TestMethod]
    public void Validate_EmptyCredential_FailsOnPassword()
    {
        LoginRequest request = new() { Email = "funcionario1@expensehub.local", Password = string.Empty };

        Assert.Contains(nameof(LoginRequest.Password), DtoValidator.InvalidMembers(request));
    }
}
