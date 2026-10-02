using System.ComponentModel;

using ModelContextProtocol.Server;

using MyFinance.Application.Dashboard;
using MyFinance.Application.Reports;
using MyFinance.Domain.Analysis;

using static MyFinance.Mcp.Tools.McpArgs;

namespace MyFinance.Mcp.Tools;

/// <summary>Visão do mês e relatórios por período.</summary>
[McpServerToolType]
internal static class ResumoTools
{
    [McpServerTool(Name = "resumo_mes", Title = "Resumo do mês", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Visão geral dos gastos de um mês de fatura: total, média e variação, faturas abertas, próximas faturas, gasto por "
        + "categoria principal, maiores aumentos e reduções, evolução mensal, parcelas a faturar, possíveis oportunidades de economia, "
        + "meta e observações. É o melhor ponto de partida para analisar a situação.")]
    public static async Task<ResumoMes> ResumoMesAsync(
        DashboardService dashboard,
        [Description("Mês da fatura (competência) no formato aaaa-MM. Vazio = fatura atual.")] string? mes = null,
        [Description("Quantidade de meses na evolução (3 a 24).")] int mesesEvolucao = DashboardService.DefaultEvolutionMonths,
        CancellationToken cancellationToken = default)
    {
        var d = await dashboard.GetAsync(ParseMonth(mes, nameof(mes)), Math.Clamp(mesesEvolucao, 3, 24), cancellationToken);

        return new ResumoMes(
            Month(d.ReferenceMonth),
            d.IsPartial,
            d.MonthSpending,
            d.Average,
            d.Variation,
            d.HistoryMonths,
            d.CurrentInvoiceTotal,
            [.. d.OpenInvoices.Select(i => new FaturaAberta(
                i.CreditCardId, i.CreditCardName, Month(i.Invoice.ReferenceMonth), Date(i.Invoice.ClosingDate), Date(i.Invoice.DueDate), i.Invoice.Amount))],
            [.. d.NextInvoices.Select(n => new ProximaFatura(Month(n.Month), n.Posted, n.Installments, n.Total))],
            [.. d.Categories.Select(c => new CategoriaValor(c.CategoryId, c.Name, c.Amount, c.Percent, c.VersusPreviousMonth, c.VersusAverage))],
            Variations(d.IncreasedVsPreviousMonth),
            Variations(d.DecreasedVsPreviousMonth),
            Variations(d.IncreasedVsAverage),
            Variations(d.DecreasedVsAverage),
            [.. d.Evolution.Select(e => new MesValor(Month(e.Month), e.Amount))],
            d.OutstandingInstallments,
            [.. d.Opportunities.Select(o => new Oportunidade(o.Title, o.Detail, o.MonthlyPotential))],
            d.Goal is { } goal ? new MetaProgresso(goal.Name, goal.MonthlyTarget, goal.IdentifiedPotential) : null,
            [.. d.Insights.Select(i => new Insight(i.Tone, i.Message))]);
    }

    [McpServerTool(Name = "gastos_por_categoria", Title = "Gastos por categoria", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Total gasto por categoria em um período de meses de fatura, com participação, média mensal e variação do último mês "
        + "em relação ao anterior. \"Sem categoria\" aparece com categoriaId nulo.")]
    public static async Task<Periodo<CategoriaRelatorio>> GastosPorCategoriaAsync(
        ReportService reports,
        [Description("Primeiro mês (aaaa-MM). Vazio = 3 meses até a fatura atual.")] string? de = null,
        [Description("Último mês (aaaa-MM). Vazio = fatura atual.")] string? ate = null,
        [Description("true = detalha por subcategoria; false = agrupa nas categorias principais.")] bool incluirSubcategorias = false,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = await PeriodAsync(reports, de, ate, 3, cancellationToken);
        var rows = await reports.ByCategoryAsync(from, to, incluirSubcategorias ? CategoryLevel.Leaf : CategoryLevel.Root, cancellationToken);
        return new(Month(from), Month(to), [.. rows.Select(r => new CategoriaRelatorio(r.CategoryId, r.Name, r.Total, r.Percent, r.MonthlyAverage, r.Variation))]);
    }

    [McpServerTool(Name = "gastos_por_estabelecimento", Title = "Gastos por estabelecimento", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Estabelecimentos com maior gasto em um período: quantidade de compras, total e ticket médio.")]
    public static async Task<Periodo<EstabelecimentoRelatorio>> GastosPorEstabelecimentoAsync(
        ReportService reports,
        [Description("Primeiro mês (aaaa-MM). Vazio = 3 meses até a fatura atual.")] string? de = null,
        [Description("Último mês (aaaa-MM). Vazio = fatura atual.")] string? ate = null,
        [Description("Máximo de estabelecimentos (1 a 100).")] int limite = 30,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = await PeriodAsync(reports, de, ate, 3, cancellationToken);
        var rows = await reports.ByMerchantAsync(from, to, cancellationToken);
        return new(Month(from), Month(to), [.. rows.Take(Math.Clamp(limite, 1, ReportService.MaxMerchants))
            .Select(r => new EstabelecimentoRelatorio(r.Merchant, r.Count, r.Total, r.Average))]);
    }

    [McpServerTool(Name = "evolucao_mensal", Title = "Evolução mensal", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Gasto total de cada mês de fatura no período, com a média dos meses anteriores e a variação em relação ao mês anterior.")]
    public static async Task<Periodo<MesRelatorio>> EvolucaoMensalAsync(
        ReportService reports,
        [Description("Primeiro mês (aaaa-MM). Vazio = 12 meses até a fatura atual.")] string? de = null,
        [Description("Último mês (aaaa-MM). Vazio = fatura atual.")] string? ate = null,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = await PeriodAsync(reports, de, ate, 12, cancellationToken);
        var rows = await reports.EvolutionAsync(from, to, cancellationToken);
        return new(Month(from), Month(to), [.. rows.Select(r => new MesRelatorio(Month(r.Month), r.Total, r.Average, r.Variation))]);
    }

    private static IReadOnlyList<Variacao> Variations(IReadOnlyList<VariationDto> items) =>
        [.. items.Select(v => new Variacao(v.CategoryId, v.Name, v.Current, v.Baseline, v.Difference, v.Percent))];
}