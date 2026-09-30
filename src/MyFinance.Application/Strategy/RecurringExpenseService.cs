using MyFinance.Application.Analysis;
using MyFinance.Application.Common;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Analysis;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Strategy;

public sealed record RecurringExpenseDto(
    Guid Id,
    string Name,
    decimal MonthlyAmount,
    decimal AnnualAmount,
    string? CategoryName,
    RecurringClassification Classification,
    bool IsDismissed,
    DateOnly LastSeenMonth,
    int MonthsSeen);

/// <param name="MonthlyTotal">Soma dos recorrentes não descartados.</param>
/// <param name="OptionalMonthly">Parte marcada como Opcional ou Avaliar.</param>
public sealed record RecurringOverviewDto(
    IReadOnlyList<RecurringExpenseDto> Items, decimal MonthlyTotal, decimal AnnualTotal, decimal OptionalMonthly);

/// <summary>Gastos recorrentes (assinaturas, academia...) detectados no histórico e classificados pelo usuário.</summary>
public sealed class RecurringExpenseService(
    IRecurringExpenseRepository recurring,
    AnalysisLoader loader,
    SavingsAnalysis savings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>Detecta os recorrentes da fatura atual, grava novidades (preservando classificações) e devolve a visão geral.</summary>
    public async Task<RecurringOverviewDto> GetAsync(bool includeDismissed, CancellationToken cancellationToken)
    {
        var month = await loader.DefaultMonthAsync(cancellationToken);
        var context = await loader.LoadAsync(month, RecurringExpenseDetector.WindowMonths - 1, 0, cancellationToken);
        var items = await savings.RecurringAsync(context, cancellationToken);
        var now = timeProvider.UtcNow();

        var saved = new Dictionary<string, RecurringExpense>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var c = item.Candidate;
            var entity = item.Saved;
            if (entity is null)
            {
                entity = RecurringExpense.Create(c.MerchantKey, c.DisplayName, c.EstimatedMonthlyAmount, c.CategoryId, c.LastSeenMonth, now);
                recurring.Add(entity);
            }
            else
            {
                entity.Refresh(c.DisplayName, c.EstimatedMonthlyAmount, c.CategoryId, c.LastSeenMonth, now);
            }

            saved[c.MerchantKey] = entity;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var summary = SavingsAnalysis.Summarize(items);
        var list = items
            .Where(i => includeDismissed || !i.IsDismissed)
            .Select(i =>
            {
                var entity = saved[i.Candidate.MerchantKey];
                var category = i.Candidate.CategoryId is null ? null : context.Categories.Resolve(i.Candidate.CategoryId, CategoryLevel.Leaf).Name;
                return new RecurringExpenseDto(
                    entity.Id, entity.DisplayName, entity.EstimatedMonthlyAmount, entity.EstimatedAnnualAmount, category,
                    entity.Classification, entity.IsDismissed, entity.LastSeenMonth, i.Candidate.MonthsSeen);
            })
            .OrderByDescending(r => r.MonthlyAmount)
            .ToList();

        return new RecurringOverviewDto(list, summary.TotalMonthly, summary.TotalMonthly * 12, summary.OptionalMonthly);
    }

    public Task ClassifyAsync(Guid id, RecurringClassification classification, CancellationToken cancellationToken) =>
        ChangeAsync(id, r => r.Classify(classification, timeProvider.UtcNow()), cancellationToken);

    /// <summary>"Não é recorrente": deixa de aparecer e de contar nas oportunidades.</summary>
    public Task DismissAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, r => r.Dismiss(timeProvider.UtcNow()), cancellationToken);

    public Task RestoreAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, r => r.Restore(timeProvider.UtcNow()), cancellationToken);

    private async Task ChangeAsync(Guid id, Action<RecurringExpense> change, CancellationToken cancellationToken)
    {
        change(await recurring.GetByIdAsync(id, cancellationToken) ?? throw new ValidationException("Gasto recorrente não encontrado."));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}