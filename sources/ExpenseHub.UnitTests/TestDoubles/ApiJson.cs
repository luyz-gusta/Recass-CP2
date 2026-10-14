using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpenseHub.UnitTests.TestDoubles;

/// <summary>
/// Opções de JSON iguais às do <c>Program</c>: nomes em camelCase, enums como texto e campo desconhecido rejeitado
/// (o que impede mass assignment).
/// </summary>
internal static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
