using MyFinance.Application.Analysis;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Common;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;

namespace MyFinance.Application.Strategy;

public sealed record SaveSpendingLimitCommand(Guid CategoryId, decimal MonthlyAmount);

/// <param name="Percent">Consumo (1 = 100%).</param>
public sealed record SpendingLimitDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Color,
    decimal MonthlyAmount,
    bool IsActive,
    decimal Used,
    decimal Available,
    decimal Excess,
    decimal Percent,
    LimitStatus Status);

/// <summary>Limites mensais de gastos por categoria (a categoria principal inclui as subcategorias).</summary>
public sealed class SpendingLimitService(
    ISpendingLimitRepository limits,
    ICategoryRepository categories,
    AnalysisLoader loader,
    SavingsAnalysis savings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <param name="month">Mês analisado; <c>null</c> = fatura atual.</param>
    public async Task<IReadOnlyList<SpendingLimitDto>> ListAsync(DateOnly? month, CancellationToken cancellationToken)
    {
        var reference = month ?? await loader.DefaultMonthAsync(cancellationToken);
        var context = await loader.LoadAsync(reference, 0, 0, cancellationToken);

        return [.. (await savings.LimitsAsync(context, includeInactive: true, cancellationToken))
            .OrderByDescending(l => l.Limit.IsActive)
            .ThenByDescending(l => l.Evaluation.Percent)
            .Select(l => new SpendingLimitDto(
                l.Limit.Id, l.Limit.CategoryId, l.Category.Name, l.Category.Color, l.Limit.MonthlyAmount, l.Limit.IsActive,
                l.Evaluation.Used, l.Evaluation.Available, l.Evaluation.Excess, l.Evaluation.Percent, l.Evaluation.Status))];
    }

    public async Task<Guid> CreateAsync(SaveSpendingLimitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await categories.GetByIdAsync(command.CategoryId, cancellationToken)
            ?? throw new ValidationException("Categoria não encontrada.");
        if (await limits.FindByCategoryAsync(category.Id, cancellationToken) is not null)
        {
            throw new ValidationException($"Já existe um limite para \"{category.Name}\". Edite o limite existente.");
        }

        var limit = SpendingLimit.Create(category, command.MonthlyAmount, timeProvider.UtcNow());
        limits.Add(limit);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return limit.Id;
    }

    public Task ChangeAmountAsync(Guid id, decimal monthlyAmount, CancellationToken cancellationToken) =>
        ChangeAsync(id, l => l.ChangeAmount(monthlyAmount), cancellationToken);

    public Task ActivateAsync(Guid id, CancellationToken cancellationToken) => ChangeAsync(id, l => l.Activate(), cancellationToken);

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken) => ChangeAsync(id, l => l.Deactivate(), cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        limits.Remove(await GetAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ChangeAsync(Guid id, Action<SpendingLimit> change, CancellationToken cancellationToken)
    {
        change(await GetAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<SpendingLimit> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await limits.GetByIdAsync(id, cancellationToken) ?? throw new ValidationException("Limite não encontrado.");
}