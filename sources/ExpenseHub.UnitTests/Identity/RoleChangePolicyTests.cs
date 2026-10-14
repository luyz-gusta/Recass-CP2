using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Regras de alteração de roles: só roles conhecidas, sem duplicatas, e o Admin não remove a própria role Admin.
/// </summary>
[TestClass]
public sealed class RoleChangePolicyTests
{
    /// <summary>Roles conhecidas são aceitas com o nome canônico, em ordem alfabética, mesmo com caixa e espaços diferentes.</summary>
    [TestMethod]
    public void Evaluate_KnownRolesWithDifferentCase_ReturnsCanonicalSortedRoles()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate([" employee ", "APPROVER"], isOwnAccount: false);

        Assert.IsTrue(result.Succeeded);
        CollectionAssert.AreEqual(new List<string> { AppRoles.Approver, AppRoles.Employee }, result.Value!.ToList());
    }

    /// <summary>Roles repetidas viram uma só.</summary>
    [TestMethod]
    public void Evaluate_DuplicatedRoles_RemovesDuplicates()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate([AppRoles.Finance, "finance", AppRoles.Finance], isOwnAccount: false);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(AppRoles.Finance, Assert.ContainsSingle(result.Value!));
    }

    /// <summary>Role desconhecida é rejeitada (nunca é criada implicitamente) e aparece na mensagem.</summary>
    [TestMethod]
    public void Evaluate_UnknownRole_ReturnsValidationNamingTheRole()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate([AppRoles.Employee, "SuperUser"], isOwnAccount: false);

        Assert.AreEqual(ServiceError.Validation, result.Error);
        Assert.IsTrue(result.ValidationErrors.ContainsKey(nameof(UpdateUserRolesRequest.Roles)));
        Assert.Contains("SuperUser", result.Message!);
    }

    /// <summary>Entradas vazias ou nulas na lista são rejeitadas.</summary>
    [TestMethod]
    public void Evaluate_BlankOrNullEntry_ReturnsValidation()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate([AppRoles.Employee, "  ", null], isOwnAccount: false);

        Assert.AreEqual(ServiceError.Validation, result.Error);
    }

    /// <summary>Lista ausente é rejeitada.</summary>
    [TestMethod]
    public void Evaluate_NullList_ReturnsValidation()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate(null, isOwnAccount: false);

        Assert.AreEqual(ServiceError.Validation, result.Error);
    }

    /// <summary>Lista vazia remove todas as roles de outro usuário.</summary>
    [TestMethod]
    public void Evaluate_EmptyListForAnotherUser_RemovesAllRoles()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate([], isOwnAccount: false);

        Assert.IsTrue(result.Succeeded);
        Assert.IsEmpty(result.Value!);
    }

    /// <summary>O Admin não pode remover a própria role Admin.</summary>
    [TestMethod]
    public void Evaluate_OwnAccountWithoutAdmin_ReturnsConflict()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate([AppRoles.Employee], isOwnAccount: true);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
    }

    /// <summary>O Admin não pode zerar as próprias roles.</summary>
    [TestMethod]
    public void Evaluate_OwnAccountWithEmptyList_ReturnsConflict()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate([], isOwnAccount: true);

        Assert.AreEqual(ServiceError.Conflict, result.Error);
    }

    /// <summary>O Admin pode acumular outras roles desde que mantenha a role Admin.</summary>
    [TestMethod]
    public void Evaluate_OwnAccountKeepingAdmin_Succeeds()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate([AppRoles.Admin, AppRoles.Employee], isOwnAccount: true);

        Assert.IsTrue(result.Succeeded);
        CollectionAssert.AreEqual(new List<string> { AppRoles.Admin, AppRoles.Employee }, result.Value!.ToList());
    }

    /// <summary>Remover a role Admin de OUTRO usuário é permitido.</summary>
    [TestMethod]
    public void Evaluate_RemovingAdminFromAnotherUser_Succeeds()
    {
        ServiceResult<IReadOnlyList<string>> result = RoleChangePolicy.Evaluate([AppRoles.Auditor], isOwnAccount: false);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(AppRoles.Auditor, Assert.ContainsSingle(result.Value!));
    }
}
