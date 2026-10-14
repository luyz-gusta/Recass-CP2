using System;
using System.Linq;
using ExpenseHub.Api.Domain;
using ExpenseHub.Api.Services;
using ExpenseHub.UnitTests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Matriz de decisão contextual (role, ownership e estado) com dados primitivos: dono, ator e todos os estados.
/// A ordem das verificações é fixa: role (403), depois rascunho alheio (404), depois recurso próprio (403).
/// </summary>
[TestClass]
public sealed class ExpenseAccessPolicyTests
{
    private const string OtherOwnerId = "someone-else";

    private static readonly ExpenseStatus[] _allStatuses = Enum.GetValues<ExpenseStatus>();

    /// <summary>O Employee dono edita ou envia o próprio rascunho.</summary>
    [TestMethod]
    public void CanManageDraft_EmployeeOwner_ReturnsAllowed()
    {
        Assert.AreEqual(AccessDecision.Allowed, ExpenseAccessPolicy.CanManageDraft(TestActors.Employee1, TestActors.Employee1.UserId));
    }

    /// <summary>Outro Employee recebe NotFound, sem revelar que o reembolso existe.</summary>
    [TestMethod]
    public void CanManageDraft_OtherEmployee_ReturnsNotFound()
    {
        Assert.AreEqual(AccessDecision.NotFound, ExpenseAccessPolicy.CanManageDraft(TestActors.Employee2, TestActors.Employee1.UserId));
    }

    /// <summary>Acumular Employee com Approver ou Finance não dá acesso ao rascunho de outra pessoa.</summary>
    [TestMethod]
    public void CanManageDraft_AccumulatedRolesOnOtherOwner_ReturnsNotFound()
    {
        Assert.AreEqual(AccessDecision.NotFound, ExpenseAccessPolicy.CanManageDraft(TestActors.ApproverEmployee, OtherOwnerId));
        Assert.AreEqual(AccessDecision.NotFound, ExpenseAccessPolicy.CanManageDraft(TestActors.FinanceEmployee, OtherOwnerId));
    }

    /// <summary>Quem acumula Employee com outra role continua podendo editar e enviar o próprio rascunho.</summary>
    [TestMethod]
    public void CanManageDraft_AccumulatedRolesOnOwnDraft_ReturnsAllowed()
    {
        Assert.AreEqual(AccessDecision.Allowed, ExpenseAccessPolicy.CanManageDraft(TestActors.ApproverEmployee, TestActors.ApproverEmployee.UserId));
        Assert.AreEqual(AccessDecision.Allowed, ExpenseAccessPolicy.CanManageDraft(TestActors.FinanceEmployee, TestActors.FinanceEmployee.UserId));
    }

    /// <summary>Sem a role Employee é Forbidden, inclusive quando o identificador coincide com o do dono (a role vem primeiro).</summary>
    [TestMethod]
    [DataRow(AppRoles.Approver)]
    [DataRow(AppRoles.Finance)]
    [DataRow(AppRoles.Auditor)]
    [DataRow(AppRoles.Admin)]
    public void CanManageDraft_RoleWithoutEmployee_ReturnsForbiddenForAnyOwner(string role)
    {
        Actor actor = TestActors.Create("user-1", role);

        Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanManageDraft(actor, "user-1"));
        Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanManageDraft(actor, OtherOwnerId));
    }

    /// <summary>Usuário sem nenhuma role é Forbidden.</summary>
    [TestMethod]
    public void CanManageDraft_NoRole_ReturnsForbidden()
    {
        Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanManageDraft(TestActors.NoRole, TestActors.NoRole.UserId));
    }

    /// <summary>O Approver pode decidir sobre qualquer reembolso alheio que não seja rascunho; a transição válida é checada depois, pelo serviço.</summary>
    [TestMethod]
    public void CanDecide_ApproverOnOthersNonDraft_ReturnsAllowedForEveryStatus()
    {
        foreach (ExpenseStatus status in _allStatuses.Where(status => status != ExpenseStatus.Draft))
        {
            Assert.AreEqual(AccessDecision.Allowed, ExpenseAccessPolicy.CanDecide(TestActors.Approver, OtherOwnerId, status), $"Estado {status}.");
        }
    }

    /// <summary>O rascunho de outra pessoa é NotFound para o Approver: ele nem enxerga rascunhos.</summary>
    [TestMethod]
    public void CanDecide_ApproverOnOthersDraft_ReturnsNotFound()
    {
        Assert.AreEqual(AccessDecision.NotFound, ExpenseAccessPolicy.CanDecide(TestActors.Approver, OtherOwnerId, ExpenseStatus.Draft));
    }

    /// <summary>Autoaprovação é proibida em qualquer estado, mesmo para quem acumula Approver com Employee.</summary>
    [TestMethod]
    public void CanDecide_ApproverEmployeeOnOwnExpense_ReturnsForbiddenForEveryStatus()
    {
        foreach (ExpenseStatus status in _allStatuses)
        {
            Assert.AreEqual(
                AccessDecision.Forbidden,
                ExpenseAccessPolicy.CanDecide(TestActors.ApproverEmployee, TestActors.ApproverEmployee.UserId, status),
                $"Estado {status}.");
        }
    }

    /// <summary>Approver + Employee decide sobre o reembolso enviado de outra pessoa.</summary>
    [TestMethod]
    public void CanDecide_ApproverEmployeeOnOthersSubmitted_ReturnsAllowed()
    {
        Assert.AreEqual(AccessDecision.Allowed, ExpenseAccessPolicy.CanDecide(TestActors.ApproverEmployee, OtherOwnerId, ExpenseStatus.Submitted));
    }

    /// <summary>Approver + Employee não enxerga o rascunho alheio (NotFound).</summary>
    [TestMethod]
    public void CanDecide_ApproverEmployeeOnOthersDraft_ReturnsNotFound()
    {
        Assert.AreEqual(AccessDecision.NotFound, ExpenseAccessPolicy.CanDecide(TestActors.ApproverEmployee, OtherOwnerId, ExpenseStatus.Draft));
    }

    /// <summary>Sem a role Approver é Forbidden em qualquer combinação de dono e estado, até para o rascunho alheio (a role vem primeiro).</summary>
    [TestMethod]
    [DataRow(AppRoles.Employee)]
    [DataRow(AppRoles.Finance)]
    [DataRow(AppRoles.Auditor)]
    [DataRow(AppRoles.Admin)]
    public void CanDecide_RoleWithoutApprover_ReturnsForbiddenForEveryOwnerAndStatus(string role)
    {
        Actor actor = TestActors.Create("user-1", role);

        foreach (ExpenseStatus status in _allStatuses)
        {
            Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanDecide(actor, OtherOwnerId, status), $"Outro dono, estado {status}.");
            Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanDecide(actor, "user-1", status), $"Dono, estado {status}.");
        }
    }

    /// <summary>Usuário sem nenhuma role é Forbidden.</summary>
    [TestMethod]
    public void CanDecide_NoRole_ReturnsForbiddenForEveryStatus()
    {
        foreach (ExpenseStatus status in _allStatuses)
        {
            Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanDecide(TestActors.NoRole, OtherOwnerId, status), $"Estado {status}.");
        }
    }

    /// <summary>Acumular Approver e Finance não permite decidir sobre o próprio reembolso.</summary>
    [TestMethod]
    public void CanDecide_ApproverAndFinanceAndEmployeeOnOwnExpense_ReturnsForbidden()
    {
        Actor actor = TestActors.Create("multi-1", AppRoles.Approver, AppRoles.Finance, AppRoles.Employee);

        Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanDecide(actor, "multi-1", ExpenseStatus.Submitted));
    }

    /// <summary>O Finance pode pagar qualquer reembolso alheio que não seja rascunho; a transição válida é checada depois, pelo serviço.</summary>
    [TestMethod]
    public void CanPay_FinanceOnOthersNonDraft_ReturnsAllowedForEveryStatus()
    {
        foreach (ExpenseStatus status in _allStatuses.Where(status => status != ExpenseStatus.Draft))
        {
            Assert.AreEqual(AccessDecision.Allowed, ExpenseAccessPolicy.CanPay(TestActors.Finance, OtherOwnerId, status), $"Estado {status}.");
        }
    }

    /// <summary>O rascunho de outra pessoa é NotFound para o Finance: ele nem enxerga rascunhos.</summary>
    [TestMethod]
    public void CanPay_FinanceOnOthersDraft_ReturnsNotFound()
    {
        Assert.AreEqual(AccessDecision.NotFound, ExpenseAccessPolicy.CanPay(TestActors.Finance, OtherOwnerId, ExpenseStatus.Draft));
    }

    /// <summary>Autopagamento é proibido em qualquer estado, mesmo para quem acumula Finance com Employee.</summary>
    [TestMethod]
    public void CanPay_FinanceEmployeeOnOwnExpense_ReturnsForbiddenForEveryStatus()
    {
        foreach (ExpenseStatus status in _allStatuses)
        {
            Assert.AreEqual(
                AccessDecision.Forbidden,
                ExpenseAccessPolicy.CanPay(TestActors.FinanceEmployee, TestActors.FinanceEmployee.UserId, status),
                $"Estado {status}.");
        }
    }

    /// <summary>Finance + Employee paga o reembolso aprovado de outra pessoa.</summary>
    [TestMethod]
    public void CanPay_FinanceEmployeeOnOthersApproved_ReturnsAllowed()
    {
        Assert.AreEqual(AccessDecision.Allowed, ExpenseAccessPolicy.CanPay(TestActors.FinanceEmployee, OtherOwnerId, ExpenseStatus.Approved));
    }

    /// <summary>Sem a role Finance é Forbidden em qualquer combinação de dono e estado, até para o rascunho alheio (a role vem primeiro).</summary>
    [TestMethod]
    [DataRow(AppRoles.Employee)]
    [DataRow(AppRoles.Approver)]
    [DataRow(AppRoles.Auditor)]
    [DataRow(AppRoles.Admin)]
    public void CanPay_RoleWithoutFinance_ReturnsForbiddenForEveryOwnerAndStatus(string role)
    {
        Actor actor = TestActors.Create("user-1", role);

        foreach (ExpenseStatus status in _allStatuses)
        {
            Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanPay(actor, OtherOwnerId, status), $"Outro dono, estado {status}.");
            Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanPay(actor, "user-1", status), $"Dono, estado {status}.");
        }
    }

    /// <summary>Usuário sem nenhuma role é Forbidden.</summary>
    [TestMethod]
    public void CanPay_NoRole_ReturnsForbiddenForEveryStatus()
    {
        foreach (ExpenseStatus status in _allStatuses)
        {
            Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanPay(TestActors.NoRole, OtherOwnerId, status), $"Estado {status}.");
        }
    }

    /// <summary>Acumular Finance e Approver não permite pagar o próprio reembolso.</summary>
    [TestMethod]
    public void CanPay_FinanceAndApproverAndEmployeeOnOwnExpense_ReturnsForbidden()
    {
        Actor actor = TestActors.Create("multi-1", AppRoles.Finance, AppRoles.Approver, AppRoles.Employee);

        Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanPay(actor, "multi-1", ExpenseStatus.Approved));
    }

    /// <summary>A role que decide não é a que paga: Approver não paga e Finance não aprova, mesmo sobre reembolso alheio.</summary>
    [TestMethod]
    public void CanDecideAndCanPay_SingleRoleOnOthersExpense_AllowsOnlyItsOwnTransition()
    {
        Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanPay(TestActors.Approver, OtherOwnerId, ExpenseStatus.Approved));
        Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccessPolicy.CanDecide(TestActors.Finance, OtherOwnerId, ExpenseStatus.Submitted));
    }

    /// <summary>Uma decisão negada vira o resultado de serviço correspondente: NotFound → 404 e Forbidden → 403.</summary>
    [TestMethod]
    public void ToDenied_NotFoundAndForbidden_MapToTheMatchingServiceError()
    {
        ServiceResult<string> notFound = AccessDecision.NotFound.ToDenied<string>("não encontrado", "proibido");
        ServiceResult<string> forbidden = AccessDecision.Forbidden.ToDenied<string>("não encontrado", "proibido");

        Assert.AreEqual(ServiceError.NotFound, notFound.Error);
        Assert.AreEqual("não encontrado", notFound.Message);
        Assert.AreEqual(ServiceError.Forbidden, forbidden.Error);
        Assert.AreEqual("proibido", forbidden.Message);
    }

    /// <summary>Uma decisão permitida nunca vira falha: pedir isso é erro de programação.</summary>
    [TestMethod]
    public void ToDenied_Allowed_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => AccessDecision.Allowed.ToDenied<string>("não encontrado", "proibido"));
    }

    /// <summary>A política de leitura é coerente com a lista de roles do atributo <c>[Authorize]</c> (<c>AppRoles.ExpenseReaders</c>).</summary>
    [TestMethod]
    public void HasReadRole_EveryKnownRole_AgreesWithExpenseReadersConstant()
    {
        string[] readers = AppRoles.ExpenseReaders.Split(',');

        foreach (string role in AppRoles.All)
        {
            Actor actor = TestActors.Create("user-1", role);

            Assert.AreEqual(readers.Contains(role), ExpenseAccessPolicy.HasReadRole(actor), $"Role {role}.");
        }
    }
}
