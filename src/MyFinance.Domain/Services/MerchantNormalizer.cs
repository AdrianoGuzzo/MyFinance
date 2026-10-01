using System.Text.RegularExpressions;

namespace MyFinance.Domain.Services;

/// <summary>
/// Estabelecimento extraído da descrição.
/// <paramref name="Name"/> é para exibição; <paramref name="Key"/> é a forma normalizada usada para agrupar
/// (gastos por estabelecimento, recorrentes, parcelas da mesma compra).
/// </summary>
public sealed record MerchantInfo(string Name, string Key);

/// <summary>
/// Remove da descrição o que não identifica o estabelecimento: sufixo de parcela ("Parcela 3/12"),
/// prefixos de intermediadores de pagamento ("MP*", "PAG*", "PAYPAL *"...) e números longos no fim
/// (códigos de terminal/pedido). Heurística genérica, sem regras por banco.
/// </summary>
public static partial class MerchantNormalizer
{
    public const int MaxLength = 200;

    public static MerchantInfo Normalize(string? description)
    {
        var original = (description ?? string.Empty).Trim();
        var text = InstallmentParser.Parse(original)?.BaseDescription ?? original;

        text = GatewayPrefix().Replace(text, string.Empty);
        text = TrailingNumber().Replace(text, string.Empty);
        text = Spaces().Replace(text, " ").Trim(' ', '-', '*', '.', ',');

        if (text.Length == 0)
        {
            text = original;
        }

        if (text.Length > MaxLength)
        {
            text = text[..MaxLength].TrimEnd();
        }

        return new MerchantInfo(text, TransactionFingerprint.NormalizeDescription(text));
    }

    [GeneratedRegex(@"^\s*(?:MP|MERCADOPAGO|MERCADO\s*PAGO|PAG|PAGSEGURO|PG|PAYPAL|EC|SUMUP|IZ|STONE|EBANX|PICPAY|DL|CIELO|GETNET)\s*\*\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GatewayPrefix();

    [GeneratedRegex(@"\s+\d{5,}\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNumber();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Spaces();
}