using System;
using System.Collections.Generic;

namespace ExpenseHub.Api.Services;

/// <summary>
/// Fábricas de <see cref="ServiceResult{T}"/>.
/// </summary>
public static class ServiceResult
{
    private static readonly IReadOnlyDictionary<string, string[]> _noValidationErrors = new Dictionary<string, string[]>();

    /// <summary>Cria um resultado de sucesso.</summary>
    /// <typeparam name="T">Tipo do valor.</typeparam>
    /// <param name="value">Valor produzido pela operação.</param>
    /// <returns>Resultado de sucesso.</returns>
    public static ServiceResult<T> Success<T>(T value)
    {
        return new ServiceResult<T>(value, ServiceError.None, null, _noValidationErrors);
    }

    /// <summary>Cria uma falha de recurso inexistente ou fora do escopo (HTTP 404).</summary>
    /// <typeparam name="T">Tipo do valor esperado em caso de sucesso.</typeparam>
    /// <param name="message">Mensagem para o cliente.</param>
    /// <returns>Resultado de falha.</returns>
    public static ServiceResult<T> NotFound<T>(string message)
    {
        return new ServiceResult<T>(default, ServiceError.NotFound, message, _noValidationErrors);
    }

    /// <summary>Cria uma falha de permissão (HTTP 403).</summary>
    /// <typeparam name="T">Tipo do valor esperado em caso de sucesso.</typeparam>
    /// <param name="message">Mensagem para o cliente.</param>
    /// <returns>Resultado de falha.</returns>
    public static ServiceResult<T> Forbidden<T>(string message)
    {
        return new ServiceResult<T>(default, ServiceError.Forbidden, message, _noValidationErrors);
    }

    /// <summary>Cria uma falha de conflito com o estado atual (HTTP 409).</summary>
    /// <typeparam name="T">Tipo do valor esperado em caso de sucesso.</typeparam>
    /// <param name="message">Mensagem para o cliente.</param>
    /// <returns>Resultado de falha.</returns>
    public static ServiceResult<T> Conflict<T>(string message)
    {
        return new ServiceResult<T>(default, ServiceError.Conflict, message, _noValidationErrors);
    }

    /// <summary>Cria uma falha de validação de um único campo (HTTP 400).</summary>
    /// <typeparam name="T">Tipo do valor esperado em caso de sucesso.</typeparam>
    /// <param name="field">Nome do campo inválido, como aparece no JSON.</param>
    /// <param name="message">Descrição do problema.</param>
    /// <returns>Resultado de falha.</returns>
    public static ServiceResult<T> Invalid<T>(string field, string message)
    {
        Dictionary<string, string[]> errors = new(StringComparer.Ordinal) { [field] = [message] };
        return new ServiceResult<T>(default, ServiceError.Validation, message, errors);
    }

    /// <summary>Cria uma falha de validação com erros em vários campos (HTTP 400).</summary>
    /// <typeparam name="T">Tipo do valor esperado em caso de sucesso.</typeparam>
    /// <param name="errors">Mensagens de erro por campo.</param>
    /// <param name="message">Resumo do problema.</param>
    /// <returns>Resultado de falha.</returns>
    public static ServiceResult<T> Invalid<T>(IReadOnlyDictionary<string, string[]> errors, string message)
    {
        return new ServiceResult<T>(default, ServiceError.Validation, message, errors);
    }
}

/// <summary>
/// Resultado de uma operação de serviço: um valor em caso de sucesso ou um erro tipado que o controller traduz para HTTP.
/// </summary>
/// <typeparam name="T">Tipo do valor devolvido em caso de sucesso.</typeparam>
public sealed class ServiceResult<T>
{
    internal ServiceResult(T? value, ServiceError error, string? message, IReadOnlyDictionary<string, string[]> validationErrors)
    {
        Value = value;
        Error = error;
        Message = message;
        ValidationErrors = validationErrors;
    }

    /// <summary>Indica se a operação foi concluída com sucesso.</summary>
    public bool Succeeded => Error == ServiceError.None;

    /// <summary>Valor produzido pela operação (somente em caso de sucesso).</summary>
    public T? Value { get; }

    /// <summary>Tipo de erro; <see cref="ServiceError.None"/> em caso de sucesso.</summary>
    public ServiceError Error { get; }

    /// <summary>Mensagem legível que descreve o erro.</summary>
    public string? Message { get; }

    /// <summary>Erros de validação por campo (preenchido quando <see cref="Error"/> é <see cref="ServiceError.Validation"/>).</summary>
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    /// <summary>
    /// Propaga esta falha para um resultado de outro tipo.
    /// </summary>
    /// <typeparam name="TOther">Tipo do resultado de destino.</typeparam>
    /// <returns>Resultado com o mesmo erro, mensagem e erros de validação.</returns>
    /// <exception cref="InvalidOperationException">O resultado atual é um sucesso.</exception>
    public ServiceResult<TOther> ToFailure<TOther>()
    {
        if (Succeeded)
        {
            throw new InvalidOperationException("Somente uma falha pode ser propagada para outro tipo de resultado.");
        }

        return new ServiceResult<TOther>(default, Error, Message, ValidationErrors);
    }
}
