using MyFinance.Application.Common;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Interfaces;
using MyFinance.Domain.Services;

namespace MyFinance.Application.Transactions;

/// <summary>
/// "Deseja aplicar essa regra para futuras transações semelhantes?" — oferecida após uma classificação manual.
/// </summary>
/// <param name="Pattern">Estabelecimento normalizado do lançamento classificado.</param>
/// <param name="MatchingUncategorized">Lançamentos ainda sem categoria que a regra também categorizaria.</param>
public sealed record RuleSuggestion(string Pattern, Guid CategoryId, string CategoryName, int MatchingUncategorized);

public sealed class TransactionService(
    ITransactionRepository transactions,
    ICategoryRepository categories,
    ICategoryRuleRepository rules,
    ITransactionQueries queries,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<PagedResult<TransactionListItem>> SearchAsync(TransactionSearch search, CancellationToken cancellationToken) =>
        queries.SearchAsync(search, cancellationToken);

    /// <summary>Atribui a categoria ao lançamento; <paramref name="categoryId"/> nulo remove a categoria.</summary>
    /// <returns>
    /// Sugestão de regra para classificar automaticamente lançamentos semelhantes, quando nenhuma regra ativa
    /// já leva este estabelecimento para a categoria escolhida; <c>null</c> caso contrário.
    /// </returns>
    public async Task<RuleSuggestion?> CategorizeAsync(Guid transactionId, Guid? categoryId, CancellationToken cancellationToken)
    {
        var transaction = await GetAsync(transactionId, cancellationToken);
        var now = timeProvider.UtcNow();

        if (categoryId is not { } id)
        {
            transaction.RemoveCategory(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        var category = await categories.GetByIdAsync(id, cancellationToken)
            ?? throw new ValidationException("Categoria não encontrada.");
        transaction.Categorize(category, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await SuggestRuleAsync(transaction, category, cancellationToken);
    }

    /// <summary>Altera o tipo (compra, estorno, pagamento...). O tipo precisa ser compatível com o sinal do valor.</summary>
    public async Task ChangeKindAsync(Guid transactionId, TransactionKind kind, CancellationToken cancellationToken)
    {
        var transaction = await GetAsync(transactionId, cancellationToken);
        transaction.ChangeKind(kind, timeProvider.UtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<RuleSuggestion?> SuggestRuleAsync(Transaction transaction, Category category, CancellationToken cancellationToken)
    {
        if (transaction.Kind == TransactionKind.Payment || transaction.MerchantKey.Length < 2)
        {
            return null;
        }

        var existing = CategoryRuleMatcher.Match(await rules.ListAsync(includeInactive: false, cancellationToken), transaction.Description);
        if (existing?.CategoryId == category.Id)
        {
            return null;
        }

        var pattern = transaction.MerchantKey[..Math.Min(transaction.MerchantKey.Length, CategoryRule.PatternMaxLength)].TrimEnd();
        var candidate = CategoryRule.Create(pattern, category, priority: 0, timeProvider.UtcNow());
        var matching = (await transactions.ListUncategorizedAsync(cancellationToken))
            .Count(t => candidate.Matches(TransactionFingerprint.NormalizeDescription(t.Description)));

        var parent = category.ParentCategoryId is { } parentId ? await categories.GetByIdAsync(parentId, cancellationToken) : null;
        var name = parent is null ? category.Name : $"{parent.Name} > {category.Name}";
        return new RuleSuggestion(candidate.Pattern, category.Id, name, matching);
    }

    private async Task<Transaction> GetAsync(Guid transactionId, CancellationToken cancellationToken) =>
        await transactions.GetByIdAsync(transactionId, cancellationToken)
            ?? throw new ValidationException("Lançamento não encontrado.");
}