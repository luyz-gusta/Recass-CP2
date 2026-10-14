using System;
using System.Security.Claims;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Http;
using ExpenseHub.Api.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Http;

/// <summary>
/// O ator das operações vem sempre do token: identificador e todas as roles.
/// </summary>
[TestClass]
public sealed class ClaimsPrincipalExtensionsTests
{
    /// <summary>Identificador e todas as roles do token são preservados.</summary>
    [TestMethod]
    public void ToActor_PrincipalWithIdAndRoles_ReturnsActorWithAllRoles()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, "user-42"),
            new Claim(ClaimTypes.Role, AppRoles.Employee),
            new Claim(ClaimTypes.Role, AppRoles.Approver));

        Actor actor = principal.ToActor();

        Assert.AreEqual("user-42", actor.UserId);
        Assert.HasCount(2, actor.Roles);
        Assert.IsTrue(actor.IsInRole(AppRoles.Employee));
        Assert.IsTrue(actor.IsInRole(AppRoles.Approver));
    }

    /// <summary>Usuário autenticado sem role vira um ator sem roles.</summary>
    [TestMethod]
    public void ToActor_PrincipalWithoutRoles_ReturnsActorWithoutRoles()
    {
        ClaimsPrincipal principal = CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "user-7"));

        Actor actor = principal.ToActor();

        Assert.IsEmpty(actor.Roles);
    }

    /// <summary>Token sem identificador do usuário é rejeitado.</summary>
    [TestMethod]
    public void ToActor_PrincipalWithoutNameIdentifier_Throws()
    {
        ClaimsPrincipal principal = CreatePrincipal(new Claim(ClaimTypes.Role, AppRoles.Admin));

        Assert.ThrowsExactly<InvalidOperationException>(() => principal.ToActor());
    }

    /// <summary>Roles são comparadas pelo nome canônico, sem variação de maiúsculas.</summary>
    [TestMethod]
    public void IsInRole_DifferentCase_ReturnsFalse()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, AppRoles.Finance));

        Actor actor = principal.ToActor();

        Assert.IsTrue(actor.IsInRole(AppRoles.Finance));
        Assert.IsFalse(actor.IsInRole("finance"));
        Assert.IsFalse(actor.IsInRole(AppRoles.Approver));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
