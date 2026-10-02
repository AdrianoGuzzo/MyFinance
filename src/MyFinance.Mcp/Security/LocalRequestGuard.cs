using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using MyFinance.Mcp.Logging;

namespace MyFinance.Mcp.Security;

/// <summary>
/// Aceita apenas requisições locais e autenticadas:
/// <list type="bullet">
/// <item><c>Host</c> precisa ser 127.0.0.1 ou localhost (proteção contra DNS rebinding);</item>
/// <item><c>Origin</c>, quando presente, também precisa ser local (páginas da web não podem chamar o servidor);</item>
/// <item><c>Authorization: Bearer</c> com o token configurado (outros usuários/processos da máquina não têm acesso).</item>
/// </list>
/// </summary>
internal sealed class LocalRequestGuard(string token, ILogger logger)
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes($"Bearer {token}");

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!IsLocalHost(context.Request.Host.Host))
        {
            await RejectAsync(context, StatusCodes.Status403Forbidden, "host", "Host não permitido.");
            return;
        }

        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && !(Uri.TryCreate(origin, UriKind.Absolute, out var uri) && IsLocalHost(uri.Host)))
        {
            await RejectAsync(context, StatusCodes.Status403Forbidden, "origin", "Origem não permitida.");
            return;
        }

        var authorization = Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString());
        if (!CryptographicOperations.FixedTimeEquals(authorization, _expected))
        {
            await RejectAsync(context, StatusCodes.Status401Unauthorized, "token", "Token ausente ou inválido. Copie a configuração em MyFinance › Configurações › Servidor MCP.");
            return;
        }

        await next(context);
    }

    internal static bool IsLocalHost(string host) =>
        host.Equals("127.0.0.1", StringComparison.Ordinal) || host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

    private Task RejectAsync(HttpContext context, int status, string reason, string message)
    {
        McpLog.RequestRejected(logger, reason);
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(message, context.RequestAborted);
    }
}
