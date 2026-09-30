using System.Globalization;

namespace MyFinance.Domain.Analysis;

public enum InsightTone
{
    Info = 1,

    /// <summary>Algo que merece atenção (aumento de gastos).</summary>
    Attention = 2,

    /// <summary>Comportamento positivo (redução de gastos).</summary>
    Positive = 3,
}

public sealed record Insight(InsightTone Tone, string Message);

/// <summary>Dados já calculados que alimentam os insights do mês.</summary>
/// <param name="HistoryMonths">Meses usados na média (0 = sem histórico).</param>
/// <param name="VersusAverage">Categorias principais comparadas com a média.</param>
/// <param name="VersusPreviousMonth">Categorias principais comparadas com o mês anterior.</param>
/// <param name="OutstandingInstallments">Valor das parcelas ainda não lançadas.</param>
/// <param name="RecurringMonthly">Custo mensal estimado dos gastos recorrentes.</param>
public sealed record InsightInput(
    int HistoryMonths,
    IReadOnlyList<CategoryComparison> VersusAverage,
    IReadOnlyList<CategoryComparison> VersusPreviousMonth,
    decimal OutstandingInstallments,
    int RecurringCount,
    decimal RecurringMonthly);

/// <summary>
/// Frases informativas geradas <b>exclusivamente</b> a partir dos dados existentes: cada insight só aparece quando
/// há dados suficientes (comparações com a média exigem ao menos <see cref="MinimumHistoryForAverage"/> meses de histórico).
/// Os insights informam; não recomendam nem decidem nada pelo usuário.
/// </summary>
public static class InsightGenerator
{
    public const int MinimumHistoryForAverage = 3;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static IReadOnlyList<Insight> Generate(InsightInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var insights = new List<Insight>();
        var enoughHistory = input.HistoryMonths >= MinimumHistoryForAverage;

        if (enoughHistory && VariationAnalyzer.Increased(input.VersusAverage).FirstOrDefault(c => c.Percent is not null) is { } increase)
        {
            insights.Add(new(InsightTone.Attention,
                $"Seu gasto com {increase.Category.Name} aumentou {Percent(increase.Percent!.Value)} em relação à média dos últimos {input.HistoryMonths} meses."));
        }

        if (input.HistoryMonths >= 1 && VariationAnalyzer.Increased(input.VersusPreviousMonth) is [var biggest, ..])
        {
            insights.Add(new(InsightTone.Info, $"O maior aumento de gastos neste mês ocorreu na categoria {biggest.Category.Name}."));
        }

        if (enoughHistory && VariationAnalyzer.Decreased(input.VersusAverage).FirstOrDefault(c => c.Percent is not null) is { } decrease)
        {
            insights.Add(new(InsightTone.Positive,
                $"Seu gasto com {decrease.Category.Name} diminuiu {Percent(-decrease.Percent!.Value)} em relação à média dos últimos {input.HistoryMonths} meses."));
        }

        if (input.OutstandingInstallments > 0)
        {
            insights.Add(new(InsightTone.Info,
                $"Você possui {Money(input.OutstandingInstallments)} em compras parceladas ainda não finalizadas."));
        }

        if (input.RecurringCount > 0)
        {
            insights.Add(new(InsightTone.Info,
                $"Suas assinaturas e gastos recorrentes ({input.RecurringCount}) representam aproximadamente {Money(input.RecurringMonthly)}/mês."));
        }

        if (enoughHistory && input.VersusAverage.Where(c => c.Category.Id is not null).MaxBy(c => c.Baseline) is { Baseline: > 0 } top)
        {
            insights.Add(new(InsightTone.Info,
                $"Seu gasto médio mensal com {top.Category.Name} nos últimos {input.HistoryMonths} meses foi de {Money(top.Baseline)}."));
        }

        return insights;
    }

    private static string Money(decimal value) => value.ToString("C", PtBr);

    private static string Percent(decimal ratio) => (ratio * 100).ToString("0", PtBr) + "%";
}