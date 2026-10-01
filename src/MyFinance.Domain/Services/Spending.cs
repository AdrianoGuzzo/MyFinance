using MyFinance.Domain.Enums;

namespace MyFinance.Domain.Services;

/// <summary>Regra única de "quanto um lançamento representa em gastos" (ADR 0012).</summary>
public static class Spending
{
    /// <summary>
    /// Compra, tarifa e juros aumentam os gastos (valor positivo); estorno reduz (valor negativo);
    /// pagamento de fatura não é gasto (zero); ajuste segue o sinal.
    /// </summary>
    public static decimal AmountOf(TransactionKind kind, decimal amount) =>
        kind == TransactionKind.Payment ? 0m : -amount;
}