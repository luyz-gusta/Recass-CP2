using System.Collections.Generic;
using ExpenseHub.Api.Http;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Http;

/// <summary>
/// Garante que cada erro de serviço vira o status HTTP do contrato, sempre com ProblemDetails.
/// </summary>
[TestClass]
public sealed class ControllerResultExtensionsTests
{
    /// <summary>Sucesso delega a resposta ao callback do controller.</summary>
    [TestMethod]
    public void ToActionResult_Success_UsesSuccessCallback()
    {
        TestController controller = new();

        IActionResult actionResult = controller.ToActionResult(ServiceResult.Success("ok"), value => controller.Ok(value));

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(actionResult);
        Assert.AreEqual("ok", ok.Value);
    }

    /// <summary>Recurso inexistente ou fora do escopo vira 404.</summary>
    [TestMethod]
    public void ToActionResult_NotFound_Returns404ProblemDetails()
    {
        AssertProblem(ServiceResult.NotFound<string>("Reembolso não encontrado."), StatusCodes.Status404NotFound, "Reembolso não encontrado.");
    }

    /// <summary>Falta de permissão contextual vira 403.</summary>
    [TestMethod]
    public void ToActionResult_Forbidden_Returns403ProblemDetails()
    {
        AssertProblem(ServiceResult.Forbidden<string>("Não é permitido aprovar o próprio reembolso."), StatusCodes.Status403Forbidden, "Não é permitido aprovar o próprio reembolso.");
    }

    /// <summary>Transição incompatível ou repetida vira 409.</summary>
    [TestMethod]
    public void ToActionResult_Conflict_Returns409ProblemDetails()
    {
        AssertProblem(ServiceResult.Conflict<string>("O reembolso já foi enviado."), StatusCodes.Status409Conflict, "O reembolso já foi enviado.");
    }

    /// <summary>Erro de validação vira 400 com os erros agrupados por campo.</summary>
    [TestMethod]
    public void ToActionResult_Validation_Returns400WithFieldErrors()
    {
        TestController controller = new();
        ServiceResult<string> result = ServiceResult.Invalid<string>("expenseDate", "A data da despesa não pode ser futura.");

        IActionResult actionResult = controller.ToActionResult(result, value => controller.Ok(value));

        BadRequestObjectResult badRequest = Assert.IsInstanceOfType<BadRequestObjectResult>(actionResult);
        ValidationProblemDetails problem = Assert.IsInstanceOfType<ValidationProblemDetails>(badRequest.Value);
        Assert.AreEqual(StatusCodes.Status400BadRequest, problem.Status);
        string[] messages = problem.Errors["expenseDate"];
        Assert.AreEqual("A data da despesa não pode ser futura.", Assert.ContainsSingle(messages));
    }

    /// <summary>A falha propagada para outro tipo mantém erro, mensagem e campos.</summary>
    [TestMethod]
    public void ToFailure_ValidationResult_KeepsErrorAndFieldErrors()
    {
        Dictionary<string, string[]> errors = new() { ["roles"] = ["Role desconhecida: Root."] };
        ServiceResult<int> original = ServiceResult.Invalid<int>(errors, "Roles inválidas.");

        ServiceResult<string> propagated = original.ToFailure<string>();

        Assert.AreEqual(ServiceError.Validation, propagated.Error);
        Assert.AreEqual("Roles inválidas.", propagated.Message);
        Assert.IsTrue(propagated.ValidationErrors.ContainsKey("roles"));
    }

    private static void AssertProblem(ServiceResult<string> result, int expectedStatus, string expectedDetail)
    {
        TestController controller = new();

        IActionResult actionResult = controller.ToActionResult(result, value => controller.Ok(value));

        ObjectResult objectResult = Assert.IsInstanceOfType<ObjectResult>(actionResult);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(objectResult.Value);
        Assert.AreEqual(expectedStatus, objectResult.StatusCode);
        Assert.AreEqual(expectedStatus, problem.Status);
        Assert.AreEqual(expectedDetail, problem.Detail);
    }

    private sealed class TestController : ControllerBase
    {
    }
}
