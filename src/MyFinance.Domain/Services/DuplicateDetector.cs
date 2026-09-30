using MyFinance.Domain.Enums;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Domain.Services;

/// <summary>Transação lida do arquivo, candidata a ser criada.</summary>
public sealed record DuplicateCandidate(
    DateOnly Date,
    decimal Amount,
    string Description,
    string? ExternalId,
    Sha256Hash ImportHash);

/// <summary>Transação já persistida na mesma conta/cartão.</summary>
public sealed record ExistingTransaction(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Description,
    string? ExternalId,
    Sha256Hash? ImportHash);

public sealed record DuplicateCheck(DuplicateReason Reason, Guid? ExistingTransactionId)
{
    public static DuplicateCheck NotDuplicate { get; } = new(DuplicateReason.None, null);

    public bool IsDuplicate => Reason != DuplicateReason.None;
}

/// <summary>
/// Identifica quais candidatas já existem, aplicando as estratégias em ordem de prioridade:
/// <list type="number">
/// <item><description><see cref="DuplicateReason.ExternalId"/> — identificador do banco;</description></item>
/// <item><description><see cref="DuplicateReason.ImportHash"/> — hash dos dados relevantes;</description></item>
/// <item><description><see cref="DuplicateReason.SameData"/> — data + valor + descrição normalizada.</description></item>
/// </list>
/// Cada transação existente só pode ser "consumida" por uma candidata. Assim, se o arquivo tem
/// duas compras idênticas e o banco só tem uma, a segunda é considerada nova.
/// Quando candidata e existente possuem <c>ExternalId</c> diferentes, o banco afirma que são
/// transações distintas e elas nunca são casadas pelas estratégias 2 e 3.
/// </summary>
public static class DuplicateDetector
{
    /// <param name="candidates">Transações do arquivo, na ordem em que aparecem.</param>
    /// <param name="existing">Transações já persistidas da mesma conta/cartão (e período) do arquivo.</param>
    /// <returns>Um resultado por candidata, na mesma ordem.</returns>
    public static IReadOnlyList<DuplicateCheck> Detect(
        IReadOnlyList<DuplicateCandidate> candidates,
        IReadOnlyCollection<ExistingTransaction> existing)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(existing);

        var results = new DuplicateCheck?[candidates.Count];
        var consumed = new HashSet<Guid>();

        MatchByExternalId(candidates, existing, results, consumed);

        MatchBy(
            candidates,
            existing,
            results,
            consumed,
            DuplicateReason.ImportHash,
            c => c.ImportHash.Value,
            e => e.ImportHash?.Value);

        MatchBy(
            candidates,
            existing,
            results,
            consumed,
            DuplicateReason.SameData,
            c => SameDataKey(c.Date, c.Amount, c.Description),
            e => SameDataKey(e.Date, e.Amount, e.Description));

        return [.. results.Select(r => r ?? DuplicateCheck.NotDuplicate)];
    }

    /// <summary>
    /// Casamento pelo identificador do banco. Se o identificador é único (no arquivo e no banco), basta o valor
    /// coincidir — o banco pode ter alterado data ou descrição. Se é ambíguo (repetido no arquivo ou no banco,
    /// como em bancos que reutilizam FITIDs), exige também os mesmos dados; sem isso, transações novas seriam descartadas.
    /// </summary>
    private static void MatchByExternalId(
        IReadOnlyList<DuplicateCandidate> candidates,
        IReadOnlyCollection<ExistingTransaction> existing,
        DuplicateCheck?[] results,
        HashSet<Guid> consumed)
    {
        var existingByExternalId = existing
            .Where(e => e.ExternalId is not null)
            .GroupBy(e => e.ExternalId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var occurrencesInFile = candidates
            .Where(c => c.ExternalId is not null)
            .GroupBy(c => c.ExternalId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        // Repetição no arquivo exige mesmo ExternalId E mesmos dados.
        var seenInFile = new HashSet<(string ExternalId, string DataKey)>();

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.ExternalId is not { } externalId)
            {
                continue;
            }

            var dataKey = SameDataKey(candidate.Date, candidate.Amount, candidate.Description);
            if (!seenInFile.Add((externalId, dataKey)))
            {
                results[i] = new DuplicateCheck(DuplicateReason.RepeatedInFile, null);
                continue;
            }

            if (!existingByExternalId.TryGetValue(externalId, out var sameId))
            {
                continue;
            }

            var ambiguous = sameId.Count > 1 || occurrencesInFile[externalId] > 1;
            var match = sameId.FirstOrDefault(e => !consumed.Contains(e.Id) && (ambiguous
                ? SameDataKey(e.Date, e.Amount, e.Description) == dataKey
                : e.Amount == candidate.Amount));

            if (match is not null)
            {
                results[i] = new DuplicateCheck(DuplicateReason.ExternalId, match.Id);
                consumed.Add(match.Id);
            }
        }
    }

    private static void MatchBy(
        IReadOnlyList<DuplicateCandidate> candidates,
        IReadOnlyCollection<ExistingTransaction> existing,
        DuplicateCheck?[] results,
        HashSet<Guid> consumed,
        DuplicateReason reason,
        Func<DuplicateCandidate, string> candidateKey,
        Func<ExistingTransaction, string?> existingKey)
    {
        var pool = existing
            .Where(e => !consumed.Contains(e.Id))
            .Select(e => (Key: existingKey(e), Transaction: e))
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Transaction).ToList(), StringComparer.Ordinal);

        for (var i = 0; i < candidates.Count; i++)
        {
            if (results[i] is not null || !pool.TryGetValue(candidateKey(candidates[i]), out var matches))
            {
                continue;
            }

            var match = matches.FirstOrDefault(e => !consumed.Contains(e.Id) && !HaveConflictingExternalIds(candidates[i], e));
            if (match is not null)
            {
                results[i] = new DuplicateCheck(reason, match.Id);
                consumed.Add(match.Id);
            }
        }
    }

    private static bool HaveConflictingExternalIds(DuplicateCandidate candidate, ExistingTransaction existing) =>
        candidate.ExternalId is not null
        && existing.ExternalId is not null
        && !string.Equals(candidate.ExternalId, existing.ExternalId, StringComparison.Ordinal);

    private static string SameDataKey(DateOnly date, decimal amount, string description) =>
        FormattableString.Invariant($"{date:yyyy-MM-dd}|{amount:0.00}|{TransactionFingerprint.NormalizeDescription(description)}");
}