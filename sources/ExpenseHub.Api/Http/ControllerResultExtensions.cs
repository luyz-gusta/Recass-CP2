using System;
using System.Collections.Generic;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ExpenseHub.Api.Http;

/// <summary>
/// Traduz um <see cref="ServiceResult{T}"/> para a resposta HTTP, usando ProblemDetails em todas as falhas.
/// </summary>
internal static class ControllerResultExtensions
{
    public static IActionResult ToActionResult<T>(this ControllerBase controller, ServiceResult<T> result, Func<T, IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.Succeeded)
        {
            return onSuccess(result.Value!);
        }

        return result.Error switch
        {
            ServiceError.Validation => ToValidationProblem(controller, result),
            ServiceError.NotFound => controller.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Recurso não encontrado.",
                detail: result.Message),
            ServiceError.Forbidden => controller.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Operação não permitida para este usuário.",
                detail: result.Message),
            ServiceError.Conflict => controller.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflito com o estado atual do recurso.",
                detail: result.Message),
            _ => throw new InvalidOperationException($"Erro de serviço sem tradução HTTP: {result.Error}."),
        };
    }

    private static IActionResult ToValidationProblem<T>(ControllerBase controller, ServiceResult<T> result)
    {
        ModelStateDictionary modelState = new();
        foreach (KeyValuePair<string, string[]> fieldErrors in result.ValidationErrors)
        {
            foreach (string message in fieldErrors.Value)
            {
                modelState.AddModelError(fieldErrors.Key, message);
            }
        }

        return controller.ValidationProblem(
            detail: result.Message,
            statusCode: StatusCodes.Status400BadRequest,
            modelStateDictionary: modelState);
    }
}
