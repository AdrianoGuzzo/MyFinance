using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using ModelContextProtocol;

namespace MyFinance.Mcp;

/// <summary>Serialização dos parâmetros e resultados das ferramentas: camelCase, enums por nome, sem nulos.</summary>
internal static class McpJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // O resultado é lido pela IA, não por um navegador: acentos sem escape (ç) economizam tokens e ficam legíveis.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly();
        return options;
    }
}
