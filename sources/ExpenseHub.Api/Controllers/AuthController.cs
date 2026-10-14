using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Contracts;
using ExpenseHub.Api.Dtos;
using ExpenseHub.Api.Http;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseHub.Api.Controllers;

/// <summary>
/// Autenticação: cadastro público, login com token bearer e consulta do usuário autenticado.
/// </summary>
[ApiController]
public sealed class AuthController : ControllerBase
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IAccountService _accountService;

    /// <summary>
    /// Cria o controller de autenticação.
    /// </summary>
    /// <param name="signInManager">Gerenciador de login do Identity.</param>
    /// <param name="accountService">Serviço de cadastro de usuários.</param>
    public AuthController(SignInManager<IdentityUser> signInManager, IAccountService accountService)
    {
        _signInManager = signInManager;
        _accountService = accountService;
    }

    /// <summary>
    /// Cadastra um usuário sem nenhuma role. As roles são atribuídas depois por um Admin.
    /// </summary>
    /// <param name="request">E-mail e senha. Campos extras, como roles, são rejeitados com 400.</param>
    /// <returns>201 com o usuário; 400 para dados inválidos; 409 para e-mail já cadastrado.</returns>
    [HttpPost("/register")]
    [AllowAnonymous]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterAsync(RegisterRequest request)
    {
        ServiceResult<UserResponse> result = await _accountService.RegisterAsync(request);
        return this.ToActionResult(result, user => StatusCode(StatusCodes.Status201Created, user));
    }

    /// <summary>
    /// Autentica o usuário e devolve um token bearer com as roles atuais.
    /// </summary>
    /// <param name="request">E-mail e senha.</param>
    /// <returns>200 com o token; 401 para credencial inválida ou conta bloqueada.</returns>
    [HttpPost("/login")]
    [AllowAnonymous]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync(LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(
            request.Email,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Credenciais inválidas.",
                detail: "E-mail ou senha incorretos, ou conta temporariamente bloqueada por excesso de tentativas.");
        }

        // O handler bearer do Identity já escreveu o token na resposta.
        return new EmptyResult();
    }

    /// <summary>
    /// Devolve o usuário autenticado e as roles presentes no token.
    /// Depois de uma alteração de roles, só um novo login atualiza este resultado.
    /// </summary>
    /// <returns>200 com id, e-mail e roles.</returns>
    [HttpGet("/api/me")]
    [Authorize]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public IActionResult GetCurrentUser()
    {
        Actor actor = User.ToActor();
        string email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        List<string> roles = actor.Roles.Order(StringComparer.Ordinal).ToList();

        return Ok(new UserResponse(actor.UserId, email, roles));
    }
}
