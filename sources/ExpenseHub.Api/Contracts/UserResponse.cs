using System.Collections.Generic;

namespace ExpenseHub.Api.Contracts;

/// <summary>
/// Dados públicos de um usuário. Nunca inclui hash de senha, stamps ou tokens.
/// </summary>
/// <param name="Id">Identificador do usuário.</param>
/// <param name="Email">E-mail, também usado como nome de usuário.</param>
/// <param name="Roles">Roles atribuídas, em ordem alfabética.</param>
public sealed record UserResponse(string Id, string Email, IReadOnlyList<string> Roles);
