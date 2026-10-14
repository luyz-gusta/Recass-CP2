using System.Collections.Generic;

namespace ExpenseHub.Api.Domain;

/// <summary>
/// Nomes canônicos das roles da aplicação.
/// </summary>
public static class AppRoles
{
    /// <summary>Consulta usuários e administra roles. Não concede acesso funcional a reembolsos.</summary>
    public const string Admin = "Admin";

    /// <summary>Cria, edita, envia e consulta os próprios reembolsos.</summary>
    public const string Employee = "Employee";

    /// <summary>Consulta reembolsos enviados e aprova ou reprova reembolsos de outras pessoas.</summary>
    public const string Approver = "Approver";

    /// <summary>Consulta reembolsos aprovados ou pagos e registra o pagamento de reembolsos de outras pessoas.</summary>
    public const string Finance = "Finance";

    /// <summary>Consulta todos os reembolsos e históricos, sem permissão de escrita.</summary>
    public const string Auditor = "Auditor";

    /// <summary>Roles com permissão de leitura de reembolsos, no formato aceito por <c>[Authorize(Roles = ...)]</c>.</summary>
    public const string ExpenseReaders = Employee + "," + Approver + "," + Finance + "," + Auditor;

    /// <summary>Todas as roles conhecidas pela aplicação.</summary>
    public static IReadOnlyList<string> All { get; } = [Admin, Employee, Approver, Finance, Auditor];
}
