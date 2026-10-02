using System.ComponentModel;

using ModelContextProtocol;
using ModelContextProtocol.Server;

using MyFinance.Application.Strategy;
using MyFinance.Domain.Enums;

using static MyFinance.Mcp.Tools.McpArgs;

namespace MyFinance.Mcp.Tools;

/// <summary>Estratégia de economia: meta, oportunidades, simulação, limites por categoria e gastos recorrentes.</summary>
[McpServerToolType]
internal static class EstrategiaTools
{
    [McpServerTool(Name = "obter_estrategia", Title = "Estratégia de economia", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Meta de economia, possíveis oportunidades (categorias acima da média, limites excedidos, recorrentes opcionais) em ordem, "
        + "quanto falta para a meta e o gasto mensal típico de cada categoria principal (base para simular_cenario). "
        + "Oportunidades são informação, não obrigação.")]
    public static async Task<Estrategia> ObterEstrategiaAsync(StrategyService strategy, CancellationToken cancellationToken = default)
    {
        var s = await strategy.GetAsync(cancellationToken);
        return new Estrategia(
            Month(s.ReferenceMonth),
            s.Goal is { } g ? new Meta(g.Id, g.Name, g.MonthlyTarget, g.AnnualTarget) : null,
            [.. s.Opportunities.Select(o => new PassoOportunidade(
                o.Source, o.CategoryId, o.Title, o.Detail, o.MonthlyPotential, o.Cumulative, o.ReachesGoal))],
            s.IdentifiedPotential,
            s.Gap,
            s.HistoryMonths,
            [.. s.Baselines.Select(b => new BaseCategoria(b.CategoryId, b.Name, b.MonthlyAmount))]);
    }

    [McpServerTool(Name = "simular_cenario", Title = "Simular cenário de economia", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Simula a economia mensal e anual reduzindo o gasto típico de categorias principais em percentuais. Não grava nada.")]
    public static async Task<Cenario> SimularCenarioAsync(
        StrategyService strategy,
        [Description("Reduções por categoria principal.")] IReadOnlyList<ReducaoCategoria> reducoes,
        CancellationToken cancellationToken = default)
    {
        var reductions = new Dictionary<Guid, decimal>();
        foreach (var r in reducoes ?? [])
        {
            if (r.Percentual is < 0 or > 1)
            {
                throw new McpException("Cada percentual de redução deve estar entre 0 e 1 (0.15 = 15%).");
            }

            reductions[r.CategoriaId] = r.Percentual;
        }

        var result = await strategy.SimulateAsync(reductions, cancellationToken);
        return new Cenario(
            result.CurrentMonthly, result.MonthlySavings, result.AnnualSavings, result.ProjectedMonthly,
            [.. result.Lines.Select(l => new LinhaCenario(l.CategoryId, l.Name, l.MonthlyAmount, l.Reduction, l.MonthlySavings))]);
    }

    [McpServerTool(Name = "listar_limites", Title = "Limites por categoria", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Limites mensais de gasto por categoria e o consumo no mês (Within < 80%, Near 80–100%, Exceeded > 100%). "
        + "O limite de uma categoria principal inclui as subcategorias.")]
    public static async Task<IReadOnlyList<Limite>> ListarLimitesAsync(
        SpendingLimitService limits,
        [Description("Mês da fatura (aaaa-MM). Vazio = fatura atual.")] string? mes = null,
        CancellationToken cancellationToken = default) =>
        [.. (await limits.ListAsync(ParseMonth(mes, nameof(mes)), cancellationToken)).Select(l => new Limite(
            l.Id, l.CategoryId, l.CategoryName, l.MonthlyAmount, l.IsActive, l.Used, l.Available, l.Excess, l.Percent, l.Status))];

    [McpServerTool(Name = "listar_recorrentes", Title = "Gastos recorrentes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Gastos recorrentes detectados no histórico (assinaturas, academia...), com valor mensal e anual e a classificação do "
        + "usuário (Essential, Optional, Evaluate, Unclassified).")]
    public static async Task<Recorrentes> ListarRecorrentesAsync(
        RecurringExpenseService recurring,
        [Description("Incluir itens marcados como \"não é recorrente\".")] bool incluirDescartados = false,
        CancellationToken cancellationToken = default)
    {
        var r = await recurring.GetAsync(incluirDescartados, cancellationToken);
        return new Recorrentes(
            [.. r.Items.Select(i => new Recorrente(
                i.Id, i.Name, i.MonthlyAmount, i.AnnualAmount, i.CategoryName, i.Classification, i.IsDismissed, Month(i.LastSeenMonth), i.MonthsSeen))],
            r.MonthlyTotal, r.AnnualTotal, r.OptionalMonthly);
    }

    [McpServerTool(Name = "definir_meta", Title = "Definir meta de economia", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Define a meta mensal de economia (substitui a meta atual, se houver).")]
    public static async Task<OkResult> DefinirMetaAsync(
        StrategyService strategy,
        DataChangeNotifier notifier,
        [Description("Nome da meta. Ex.: Reserva de emergência.")] string nome,
        [Description("Valor mensal a economizar, em reais.")] decimal valorMensal,
        CancellationToken cancellationToken = default)
    {
        await strategy.SetGoalAsync(nome, valorMensal, cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }

    [McpServerTool(Name = "remover_meta", Title = "Remover meta de economia", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Remove a meta de economia ativa.")]
    public static async Task<OkResult> RemoverMetaAsync(StrategyService strategy, DataChangeNotifier notifier, CancellationToken cancellationToken = default)
    {
        await strategy.ClearGoalAsync(cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }

    [McpServerTool(Name = "criar_limite", Title = "Criar limite de gasto", Destructive = false, OpenWorld = false)]
    [Description("Cria um limite mensal de gasto para uma categoria (um por categoria).")]
    public static async Task<IdResult> CriarLimiteAsync(
        SpendingLimitService limits,
        DataChangeNotifier notifier,
        [Description("Id da categoria.")] Guid categoriaId,
        [Description("Valor mensal, em reais.")] decimal valorMensal,
        CancellationToken cancellationToken = default)
    {
        var id = await limits.CreateAsync(new SaveSpendingLimitCommand(categoriaId, valorMensal), cancellationToken);
        notifier.NotifyChanged();
        return new IdResult(id);
    }

    [McpServerTool(Name = "alterar_limite", Title = "Alterar limite de gasto", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Altera o valor mensal de um limite existente.")]
    public static async Task<OkResult> AlterarLimiteAsync(
        SpendingLimitService limits,
        DataChangeNotifier notifier,
        [Description("Id do limite.")] Guid limiteId,
        [Description("Novo valor mensal, em reais.")] decimal valorMensal,
        CancellationToken cancellationToken = default)
    {
        await limits.ChangeAmountAsync(limiteId, valorMensal, cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }

    [McpServerTool(Name = "excluir_limite", Title = "Excluir limite de gasto", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Exclui um limite de gasto. Os lançamentos não são afetados.")]
    public static async Task<OkResult> ExcluirLimiteAsync(
        SpendingLimitService limits, DataChangeNotifier notifier, [Description("Id do limite.")] Guid limiteId, CancellationToken cancellationToken = default)
    {
        await limits.DeleteAsync(limiteId, cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }

    [McpServerTool(Name = "classificar_recorrente", Title = "Classificar gasto recorrente", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Classifica um gasto recorrente: Essential (essencial), Optional (opcional), Evaluate (avaliar) ou Unclassified. "
        + "Opcionais e a avaliar entram nas oportunidades de economia.")]
    public static async Task<OkResult> ClassificarRecorrenteAsync(
        RecurringExpenseService recurring,
        DataChangeNotifier notifier,
        [Description("Id do gasto recorrente (veja listar_recorrentes).")] Guid recorrenteId,
        [Description("Classificação.")] RecurringClassification classificacao,
        CancellationToken cancellationToken = default)
    {
        await recurring.ClassifyAsync(recorrenteId, classificacao, cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }

    [McpServerTool(Name = "descartar_recorrente", Title = "Descartar gasto recorrente", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Marca como \"não é recorrente\": deixa de aparecer e de contar nas oportunidades. Desfaça com restaurar_recorrente.")]
    public static async Task<OkResult> DescartarRecorrenteAsync(
        RecurringExpenseService recurring, DataChangeNotifier notifier,
        [Description("Id do gasto recorrente.")] Guid recorrenteId, CancellationToken cancellationToken = default)
    {
        await recurring.DismissAsync(recorrenteId, cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }

    [McpServerTool(Name = "restaurar_recorrente", Title = "Restaurar gasto recorrente", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Desfaz o descarte de um gasto recorrente.")]
    public static async Task<OkResult> RestaurarRecorrenteAsync(
        RecurringExpenseService recurring, DataChangeNotifier notifier,
        [Description("Id do gasto recorrente.")] Guid recorrenteId, CancellationToken cancellationToken = default)
    {
        await recurring.RestoreAsync(recorrenteId, cancellationToken);
        notifier.NotifyChanged();
        return new OkResult();
    }
}