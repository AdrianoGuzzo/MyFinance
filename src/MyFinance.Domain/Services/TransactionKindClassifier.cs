using MyFinance.Domain.Enums;

namespace MyFinance.Domain.Services;

/// <summary>
/// Sugere o tipo de um lançamento de cartão pelo sinal e por palavras da descrição.
/// O usuário pode alterar o tipo depois.
/// </summary>
public static class TransactionKindClassifier
{
    private static readonly string[] PaymentWords = ["PAGAMENTO", "PGTO", "PAGTO", "PAYMENT"];
    private static readonly string[] FeeWords = ["ANUIDADE", "TARIFA", "IOF", "SEGURO CARTAO", "AVALIACAO EMERGENCIAL"];
    private static readonly string[] InterestWords = ["JUROS", "ENCARGOS", "MULTA", "MORA", "ROTATIVO"];

    public static TransactionKind Classify(decimal amount, string? description)
    {
        var words = TransactionFingerprint.NormalizeDescription(description)
            .Split([' ', '*', '-', '.', '/', ',', ':'], StringSplitOptions.RemoveEmptyEntries);
        var text = string.Join(' ', words);

        if (amount > 0)
        {
            return ContainsAny(words, text, PaymentWords) ? TransactionKind.Payment : TransactionKind.Refund;
        }

        if (ContainsAny(words, text, InterestWords))
        {
            return TransactionKind.Interest;
        }

        return ContainsAny(words, text, FeeWords) ? TransactionKind.Fee : TransactionKind.Purchase;
    }

    /// <summary>Palavras simples casam por palavra inteira ("IOF" não casa com "RIOFERTIL"); expressões, pelo texto.</summary>
    private static bool ContainsAny(string[] words, string text, string[] candidates) =>
        candidates.Any(c => c.Contains(' ', StringComparison.Ordinal)
            ? text.Contains(c, StringComparison.Ordinal)
            : words.Contains(c, StringComparer.Ordinal));
}