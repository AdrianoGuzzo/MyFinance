using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Exceptions;
using MyFinance.Mcp.Logging;
using MyFinance.Mcp.Prompts;
using MyFinance.Mcp.Tools;

namespace MyFinance.Mcp;

/// <summary>Registro do servidor MCP, das ferramentas e dos roteiros.</summary>
internal static class McpComposition
{
    /// <summary>
    /// Catálogo fechado: só estas classes viram ferramentas (nunca por varredura do assembly), para que importações,
    /// cartões, faturas e exclusões não fiquem expostos por acidente.
    /// </summary>
    public static readonly Type[] ToolTypes =
        [typeof(ResumoTools), typeof(CartaoTools), typeof(TransacaoTools), typeof(CategoriaTools), typeof(EstrategiaTools)];

    public const string Instructions = """
        MyFinance: gastos de cartão de crédito do usuário, guardados localmente.
        - Valores em reais (BRL). "gasto" positivo = despesa; negativo = estorno; pagamentos de fatura não são gasto.
        - Meses são de competência da fatura (mês do vencimento), no formato aaaa-MM; datas no formato aaaa-MM-dd.
        - Ids são GUIDs. Antes de categorizar, use listar_categorias para obter os ids; prefira categorizar_transacoes_em_lote.
        - Comece análises por resumo_mes. Oportunidades de economia são informação, não obrigação.
        - Não é possível importar extratos, editar cartões, marcar faturas como pagas nem excluir lançamentos por aqui.
        """;

    public static IServiceCollection AddMyFinanceMcp(this IServiceCollection services)
    {
        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "myfinance",
                    Title = "MyFinance",
                    Version = typeof(McpComposition).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
                };
                options.ServerInstructions = Instructions;
            })
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            // O cast escolhe a sobrecarga por tipos; um Type[] cairia em WithTools<T>(T alvo), que procuraria ferramentas no próprio array.
            .WithTools((IEnumerable<Type>)ToolTypes, McpJson.Options)
            .WithPrompts([typeof(MyFinancePrompts)], McpJson.Options)
            .WithRequestFilters(filters => filters.AddCallToolFilter(MapErrors));
        return services;
    }

    /// <summary>
    /// Erros esperados (validação e regras de negócio) viram <see cref="McpException"/>, cuja mensagem amigável chega à IA.
    /// Os demais são registrados (só o nome da ferramenta) e devolvidos como erro genérico pelo SDK.
    /// </summary>
    private static McpRequestHandler<CallToolRequestParams, CallToolResult> MapErrors(McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, cancellationToken) =>
        {
            try
            {
                return await next(context, cancellationToken);
            }
            catch (AppException ex)
            {
                throw new McpException(ex.Message, ex);
            }
            catch (DomainException ex)
            {
                throw new McpException(ex.Message, ex);
            }
            catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
            {
                var logger = context.Services?.GetService<ILoggerFactory>()?.CreateLogger("MyFinance.Mcp.Tools");
                if (logger is not null)
                {
                    McpLog.ToolFailed(logger, context.Params?.Name ?? "?", ex);
                }

                throw;
            }
        };
}