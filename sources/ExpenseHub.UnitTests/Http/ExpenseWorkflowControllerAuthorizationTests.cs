using System;
using System.Linq;
using System.Reflection;
using ExpenseHub.Api.Controllers;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Http;

/// <summary>
/// A primeira camada da autorização do fluxo de aprovação: cada rota de <c>ExpenseWorkflowController</c> exige a role da matriz.
/// Ownership e estado são decididos no serviço, mas uma regressão aqui abriria a rota para outros perfis.
/// </summary>
[TestClass]
public sealed class ExpenseWorkflowControllerAuthorizationTests
{
    /// <summary>Aprovar e reprovar exigem somente a role Approver.</summary>
    [TestMethod]
    [DataRow(nameof(ExpenseWorkflowController.ApproveAsync))]
    [DataRow(nameof(ExpenseWorkflowController.RejectAsync))]
    public void DecisionAction_AuthorizeAttribute_RequiresApproverRole(string action)
    {
        Assert.AreEqual(AppRoles.Approver, RolesOf(action));
    }

    /// <summary>Registrar pagamento exige somente a role Finance.</summary>
    [TestMethod]
    public void PayAction_AuthorizeAttribute_RequiresFinanceRole()
    {
        Assert.AreEqual(AppRoles.Finance, RolesOf(nameof(ExpenseWorkflowController.PayAsync)));
    }

    /// <summary>O histórico exige uma das quatro roles de leitura; Admin não está entre elas.</summary>
    [TestMethod]
    public void HistoryAction_AuthorizeAttribute_RequiresOneOfTheReaderRoles()
    {
        string roles = RolesOf(nameof(ExpenseWorkflowController.GetHistoryAsync));

        Assert.AreEqual(AppRoles.ExpenseReaders, roles);
        Assert.DoesNotContain(AppRoles.Admin, roles.Split(','));
    }

    /// <summary>As decisões são <c>POST /api/expenses/{id}/approve</c> e <c>POST /api/expenses/{id}/reject</c>, com id GUID.</summary>
    [TestMethod]
    [DataRow(nameof(ExpenseWorkflowController.ApproveAsync), "{id:guid}/approve")]
    [DataRow(nameof(ExpenseWorkflowController.RejectAsync), "{id:guid}/reject")]
    public void DecisionAction_RouteAttribute_IsPostUnderExpenseId(string action, string template)
    {
        HttpPostAttribute? post = ActionOf(action).GetCustomAttribute<HttpPostAttribute>();

        Assert.IsNotNull(post);
        Assert.AreEqual(template, post.Template);
        Assert.AreEqual("api/expenses", typeof(ExpenseWorkflowController).GetCustomAttribute<RouteAttribute>()?.Template);
    }

    /// <summary>O pagamento é <c>POST /api/expenses/{id}/pay</c>, com id GUID.</summary>
    [TestMethod]
    public void PayAction_RouteAttribute_IsPostUnderExpenseId()
    {
        HttpPostAttribute? post = ActionOf(nameof(ExpenseWorkflowController.PayAsync)).GetCustomAttribute<HttpPostAttribute>();

        Assert.IsNotNull(post);
        Assert.AreEqual("{id:guid}/pay", post.Template);
    }

    /// <summary>O histórico é <c>GET /api/expenses/{id}/history</c>, com id GUID.</summary>
    [TestMethod]
    public void HistoryAction_RouteAttribute_IsGetUnderExpenseId()
    {
        HttpGetAttribute? get = ActionOf(nameof(ExpenseWorkflowController.GetHistoryAsync)).GetCustomAttribute<HttpGetAttribute>();

        Assert.IsNotNull(get);
        Assert.AreEqual("{id:guid}/history", get.Template);
    }

    /// <summary>Nenhuma rota do fluxo é pública e toda action declara as roles exigidas: nenhuma rota nova nasce sem atributo.</summary>
    [TestMethod]
    public void ExpenseWorkflowController_EveryAction_DeclaresRolesAndNoneIsAnonymous()
    {
        MethodInfo[] actions = typeof(ExpenseWorkflowController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.IsNotEmpty(actions);
        Assert.IsNull(typeof(ExpenseWorkflowController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.IsFalse(actions.Any(action => action.GetCustomAttribute<AllowAnonymousAttribute>() is not null));
        foreach (MethodInfo action in actions)
        {
            AuthorizeAttribute? authorize = action.GetCustomAttribute<AuthorizeAttribute>();

            Assert.IsNotNull(authorize, $"A action {action.Name} não declara [Authorize].");
            Assert.IsFalse(string.IsNullOrWhiteSpace(authorize.Roles), $"A action {action.Name} não declara as roles.");
        }
    }

    private static MethodInfo ActionOf(string actionName)
    {
        return typeof(ExpenseWorkflowController).GetMethod(actionName)
            ?? throw new InvalidOperationException($"A action {actionName} não existe.");
    }

    private static string RolesOf(string actionName)
    {
        AuthorizeAttribute authorize = ActionOf(actionName).GetCustomAttribute<AuthorizeAttribute>()
            ?? throw new InvalidOperationException($"A action {actionName} não tem [Authorize].");

        return authorize.Roles ?? string.Empty;
    }
}
