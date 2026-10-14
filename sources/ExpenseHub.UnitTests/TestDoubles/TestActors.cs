using System;
using System.Collections.Generic;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;

namespace ExpenseHub.UnitTests.TestDoubles;

/// <summary>
/// Atores prontos para os testes, cada um com um identificador distinto.
/// </summary>
internal static class TestActors
{
    public static Actor Employee1 { get; } = Create("employee-1", AppRoles.Employee);

    public static Actor Employee2 { get; } = Create("employee-2", AppRoles.Employee);

    public static Actor Approver { get; } = Create("approver-1", AppRoles.Approver);

    public static Actor ApproverEmployee { get; } = Create("approver-employee-1", AppRoles.Approver, AppRoles.Employee);

    public static Actor Finance { get; } = Create("finance-1", AppRoles.Finance);

    public static Actor FinanceEmployee { get; } = Create("finance-employee-1", AppRoles.Finance, AppRoles.Employee);

    public static Actor Auditor { get; } = Create("auditor-1", AppRoles.Auditor);

    public static Actor Admin { get; } = Create("admin-1", AppRoles.Admin);

    public static Actor NoRole { get; } = Create("no-role-1");

    public static Actor Create(string userId, params string[] roles)
    {
        return new Actor(userId, new HashSet<string>(roles, StringComparer.Ordinal));
    }
}
