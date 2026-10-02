using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.Server;

namespace MyFinance.Mcp.Tests;

/// <summary>O catálogo é fechado: nenhuma operação proibida (importar, cartões, faturas, exclusões) pode virar ferramenta.</summary>
public sealed class McpCatalogTests
{
    [Fact]
    public void Somente_as_ferramentas_permitidas_sao_registradas()
    {
        using var provider = new ServiceCollection().AddLogging().AddMyFinanceMcp().BuildServiceProvider();

        var tools = provider.GetServices<McpServerTool>().Select(t => t.ProtocolTool).ToList();

        tools.Should().HaveCount(31);
        tools.Select(t => t.Name).Should().OnlyHaveUniqueItems();
        tools.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Description) && t.Annotations!.OpenWorldHint == false);
        tools.Select(t => t.Name).Should().NotContain(n =>
            n.Contains("import", StringComparison.Ordinal) || n.Contains("pag", StringComparison.Ordinal)
            || n.Contains("excluir_trans", StringComparison.Ordinal) || n.Contains("backup", StringComparison.Ordinal)
            || (n.Contains("cartao", StringComparison.Ordinal) && !n.StartsWith("listar_", StringComparison.Ordinal)));
    }

    [Fact]
    public void Ferramentas_de_leitura_sao_marcadas_como_somente_leitura()
    {
        using var provider = new ServiceCollection().AddLogging().AddMyFinanceMcp().BuildServiceProvider();

        var tools = provider.GetServices<McpServerTool>().Select(t => t.ProtocolTool).ToList();
        var readOnly = tools.Where(t => t.Annotations!.ReadOnlyHint == true).Select(t => t.Name).ToList();

        string[] others = ["resumo_mes", "evolucao_mensal", "obter_estrategia", "simular_cenario"];
        readOnly.Should().HaveCount(15).And.OnlyContain(n =>
            n.StartsWith("listar_", StringComparison.Ordinal) || n.StartsWith("buscar_", StringComparison.Ordinal)
            || n.StartsWith("gastos_", StringComparison.Ordinal) || others.Contains(n));
    }
}