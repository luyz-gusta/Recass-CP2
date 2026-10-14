using System.Collections.Generic;
using ExpenseHub.Api.Dtos;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Validation;

/// <summary>
/// Cadastro e alteração de roles validam a entrada antes de chegar ao Identity.
/// </summary>
[TestClass]
public sealed class RegisterRequestValidationTests
{
    private const string ValidEmail = "funcionario1@expensehub.local";

    /// <summary>E-mail válido e senha entre 8 e 100 caracteres passam.</summary>
    [TestMethod]
    [DataRow(8)]
    [DataRow(100)]
    public void Validate_CredentialWithinLimits_IsValid(int length)
    {
        RegisterRequest request = new() { Email = ValidEmail, Password = new string('x', length) };

        Assert.IsEmpty(DtoValidator.Validate(request));
    }

    /// <summary>Senha vazia, curta demais ou longa demais é rejeitada.</summary>
    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    [DataRow(101)]
    public void Validate_CredentialOutsideLimits_FailsOnPassword(int length)
    {
        RegisterRequest request = new() { Email = ValidEmail, Password = new string('x', length) };

        Assert.Contains(nameof(RegisterRequest.Password), DtoValidator.InvalidMembers(request));
    }

    /// <summary>E-mail ausente ou malformado é rejeitado.</summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("funcionario1")]
    public void Validate_InvalidEmail_FailsOnEmail(string email)
    {
        RegisterRequest request = new() { Email = email, Password = new string('x', 12) };

        Assert.Contains(nameof(RegisterRequest.Email), DtoValidator.InvalidMembers(request));
    }

    /// <summary>A alteração de roles exige a lista, mesmo que vazia.</summary>
    [TestMethod]
    public void Validate_RolesListMissing_FailsOnRoles()
    {
        UpdateUserRolesRequest request = new() { Roles = null };

        Assert.Contains(nameof(UpdateUserRolesRequest.Roles), DtoValidator.InvalidMembers(request));
    }

    /// <summary>Lista vazia é uma entrada válida (remove todas as roles).</summary>
    [TestMethod]
    public void Validate_EmptyRolesList_IsValid()
    {
        UpdateUserRolesRequest request = new() { Roles = new List<string>() };

        Assert.IsEmpty(DtoValidator.Validate(request));
    }
}
