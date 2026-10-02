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

/// <param name="CategoryId">Categoria a atribuir; <c>null</c> remove a categoria.</param>
public sealed record CategorizationItem(Guid TransactionId, Guid? CategoryId);

/// <param name="Categorized">Lançamentos que receberam uma categoria.</param>
/// <param name="Uncategorized">Lançamentos que tiveram a categoria removida.</param>
public sealed record BatchCategorizationResult(int Categorized, int Uncategorized);

public sealed class TransactionService(
    ITransactionRepository transactions,
    ICategoryRepository categories,
    ICategoryRuleRepository rules,
    ITransactionQueries queries,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public const int MaxBatchSize = 500;

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

    /// <summary>
    /// Categoriza vários lançamentos de uma vez: tudo ou nada. Se algum lançamento ou categoria não existir
    /// (ou a categoria estiver desativada), nada é gravado e a mensagem lista os ids com problema.
    /// </summary>
    public async Task<BatchCategorizationResult> CategorizeManyAsync(IReadOnlyList<CategorizationItem> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ValidationException("Informe ao menos um lançamento.");
        }

        if (items.Count > MaxBatchSize)
        {
            throw new ValidationException($"Informe no máximo {MaxBatchSize} lançamentos por vez.");
        }

        var problems = new List<string>();
        var duplicated = items.GroupBy(i => i.TransactionId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicated.Count > 0)
        {
            problems.Add($"lançamentos repetidos: {string.Join(", ", duplicated)}");
        }

        var found = (await transactions.GetByIdsAsync([.. items.Select(i => i.TransactionId).Distinct()], cancellationToken))
            .ToDictionary(t => t.Id);
        var missing = items.Select(i => i.TransactionId).Distinct().Where(id => !found.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            problems.Add($"lançamentos não encontrados: {string.Join(", ", missing)}");
        }

        var categoryIds = items.Where(i => i.CategoryId is not null).Select(i => i.CategoryId!.Value).Distinct().ToList();
        var known = categoryIds.Count == 0
            ? new Dictionary<Guid, Category>()
            : (await categories.ListAsync(includeInactive: true, cancellationToken)).ToDictionary(c => c.Id);
        var unknown = categoryIds.Where(id => !known.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            problems.Add($"categorias não encontradas: {string.Join(", ", unknown)}");
        }

        var inactive = categoryIds.Where(id => known.TryGetValue(id, out var c) && !c.IsActive).ToList();
        if (inactive.Count > 0)
        {
            problems.Add($"categorias desativadas: {string.Join(", ", inactive)}");
        }

        if (problems.Count > 0)
        {
            throw new ValidationException($"Nenhum lançamento foi alterado ({string.Join("; ", problems)}).");
        }

        var now = timeProvider.UtcNow();
        var categorized = 0;
        foreach (var item in items)
        {
            var transaction = found[item.TransactionId];
            if (item.CategoryId is { } id)
            {
                transaction.Categorize(known[id], now);
                categorized++;
            }
            else
            {
                transaction.RemoveCategory(now);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new BatchCategorizationResult(categorized, items.Count - categorized);
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