using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Erros do Identity no cadastro viram o status do contrato: e-mail duplicado é 409; senha ou e-mail inválidos são 400.
/// </summary>
[TestClass]
public sealed class IdentityErrorMapperTests
{
    private static readonly IdentityErrorDescriber _describer = new();

    /// <summary>E-mail já cadastrado é conflito.</summary>
    [TestMethod]
    public void ToFailure_DuplicateEmail_ReturnsConflict()
    {
        ServiceResult<UserResponse> result = IdentityErrorMapper.ToFailure<UserResponse>(
            [_describer.DuplicateUserName("funcionario1@expensehub.local"), _describer.DuplicateEmail("funcionario1@expensehub.local")]);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
    }

    /// <summary>Violações da política de senha viram erros de validação do campo de senha.</summary>
    [TestMethod]
    public void ToFailure_PasswordPolicyErrors_ReturnsValidationOnPasswordField()
    {
        ServiceResult<UserResponse> result = IdentityErrorMapper.ToFailure<UserResponse>(
            [_describer.PasswordRequiresDigit(), _describer.PasswordRequiresUpper()]);

        Assert.AreEqual(ServiceError.Validation, result.Error);
        Assert.HasCount(2, result.ValidationErrors[nameof(RegisterRequest.Password)]);
    }

    /// <summary>E-mail recusado pelo Identity vira erro de validação do campo de e-mail.</summary>
    [TestMethod]
    public void ToFailure_InvalidEmail_ReturnsValidationOnEmailField()
    {
        ServiceResult<UserResponse> result = IdentityErrorMapper.ToFailure<UserResponse>([_describer.InvalidEmail("x@")]);

        Assert.AreEqual(ServiceError.Validation, result.Error);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(RegisterRequest.Email)));
    }
}
