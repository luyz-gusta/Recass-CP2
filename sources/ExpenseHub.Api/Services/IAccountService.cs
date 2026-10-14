using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Dtos;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Cadastro público de usuários.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Cadastra um usuário sem nenhuma role.
    /// </summary>
    /// <param name="request">E-mail e senha.</param>
    /// <returns>O usuário criado; Conflict para e-mail já cadastrado; Validation para dados fora da política do Identity.</returns>
    Task<ServiceResult<UserResponse>> RegisterAsync(RegisterRequest request);
}
