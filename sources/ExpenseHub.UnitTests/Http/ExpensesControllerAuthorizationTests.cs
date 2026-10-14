using System;
using System.Linq;
using System.Reflection;
using ExpenseHub.Api.Controllers;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Http;

/// <summary>
/// A primeira camada da autorização: cada rota de <c>ExpensesController</c> exige a role da matriz de acesso.
/// A role no atributo não basta sozinha (ownership e estado são decididos no serviço), mas uma regressão aqui abriria a rota.
/// </summary>
[TestClass]
public sealed class ExpensesControllerAuthorizationTests
{
    /// <summary>Criar, editar e enviar exigem a role Employee.</summary>
    [TestMethod]
    [DataRow(nameof(ExpensesController.CreateAsync), AppRoles.Employee)]
    [DataRow(nameof(ExpensesController.UpdateAsync), AppRoles.Employee)]
    [DataRow(nameof(ExpensesController.SubmitAsync), AppRoles.Employee)]
    public void WriteAction_AuthorizeAttribute_RequiresEmployeeRole(string action, string expectedRoles)
    {
        Assert.AreEqual(expectedRoles, RolesOf(action));
    }

    /// <summary>Listar e consultar exigem uma das quatro roles de leitura; Admin não está entre elas.</summary>
    [TestMethod]
    [DataRow(nameof(ExpensesController.ListAsync))]
    [DataRow(nameof(ExpensesController.GetAsync))]
    public void ReadAction_AuthorizeAttribute_RequiresOneOfTheReaderRoles(string action)
    {
        Assert.AreEqual(AppRoles.ExpenseReaders, RolesOf(action));
        Assert.DoesNotContain(AppRoles.Admin, RolesOf(action).Split(','));
    }

    /// <summary>Nenhuma rota de reembolsos é pública: o controller não usa <c>[AllowAnonymous]</c>.</summary>
    [TestMethod]
    public void ExpensesController_AnyAction_IsNeverAnonymous()
    {
        bool controllerIsAnonymous = typeof(ExpensesController).GetCustomAttribute<AllowAnonymousAttribute>() is not null;
        bool anyActionIsAnonymous = typeof(ExpensesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(method => method.GetCustomAttribute<AllowAnonymousAttribute>() is not null);

        Assert.IsFalse(controllerIsAnonymous);
        Assert.IsFalse(anyActionIsAnonymous);
    }

    /// <summary>Toda action pública do controller declara as roles exigidas: nenhuma rota nova nasce sem atributo.</summary>
    [TestMethod]
    public void ExpensesController_EveryAction_DeclaresRoles()
    {
        MethodInfo[] actions = typeof(ExpensesController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.IsNotEmpty(actions);
        foreach (MethodInfo action in actions)
        {
            AuthorizeAttribute? authorize = action.GetCustomAttribute<AuthorizeAttribute>();

            Assert.IsNotNull(authorize, $"A action {action.Name} não declara [Authorize].");
            Assert.IsFalse(string.IsNullOrWhiteSpace(authorize.Roles), $"A action {action.Name} não declara as roles.");
        }
    }

    private static string RolesOf(string actionName)
    {
        MethodInfo action = typeof(ExpensesController).GetMethod(actionName)
            ?? throw new InvalidOperationException($"A action {actionName} não existe.");
        AuthorizeAttribute authorize = action.GetCustomAttribute<AuthorizeAttribute>()
            ?? throw new InvalidOperationException($"A action {actionName} não tem [Authorize].");

        return authorize.Roles ?? string.Empty;
    }
}
