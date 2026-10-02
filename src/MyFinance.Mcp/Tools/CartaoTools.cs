using System.ComponentModel;

using ModelContextProtocol.Server;

using MyFinance.Application.CreditCards;
using MyFinance.Application.Installments;
using MyFinance.Application.Invoices;

using static MyFinance.Mcp.Tools.McpArgs;

namespace MyFinance.Mcp.Tools;

/// <summary>Cartões, faturas e parcelamentos (somente leitura).</summary>
[McpServerToolType]
internal static class CartaoTools
{
    [McpServerTool(Name = "listar_cartoes", Title = "Listar cartões", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Cartões de crédito cadastrados, com dias de fechamento e vencimento, limite e a fatura aberta atual.")]
    public static async Task<IReadOnlyList<Cartao>> ListarCartoesAsync(
        CreditCardService cards,
        [Description("Incluir cartões desativados.")] bool incluirInativos = false,
        CancellationToken cancellationToken = default) =>
        [.. (await cards.ListAsync(incluirInativos, cancellationToken)).Select(c => new Cartao(
            c.Id, c.Name, c.Issuer, c.Brand, c.CreditLimit, c.ClosingDay, c.DueDay, c.IsActive,
            new FaturaAtual(Month(c.CurrentInvoice.ReferenceMonth), Date(c.CurrentInvoice.ClosingDate), Date(c.CurrentInvoice.DueDate), c.CurrentInvoice.Amount),
            c.LimitUsage))];

    [McpServerTool(Name = "listar_faturas", Title = "Listar faturas", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Faturas da mais recente para a mais antiga, com total (gastos), pagamentos lançados, quantidade de lançamentos e situação.")]
    public static async Task<IReadOnlyList<Fatura>> ListarFaturasAsync(
        InvoiceService invoices,
        [Description("Id do cartão. Vazio = todos os cartões.")] Guid? cartaoId = null,
        [Description("Máximo de faturas (1 a 60).")] int limite = 12,
        CancellationToken cancellationToken = default) =>
        [.. (await invoices.ListAsync(cartaoId, cancellationToken)).Take(Math.Clamp(limite, 1, 60)).Select(i => new Fatura(
            i.Id, i.CreditCardId, i.CreditCardName, Month(i.ReferenceMonth), Date(i.StartDate), Date(i.ClosingDate), Date(i.DueDate), i.Status,
            i.Total, i.Payments, i.TransactionCount))];

    [McpServerTool(Name = "listar_parcelamentos", Title = "Listar parcelamentos", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Compras parceladas (parcela atual, restantes e valor restante) e o valor de parcelas já comprometido nas próximas faturas.")]
    public static async Task<Parcelamentos> ListarParcelamentosAsync(
        InstallmentService installments,
        [Description("Incluir compras cujas parcelas já terminaram.")] bool incluirFinalizados = false,
        [Description("Meses de projeção dos compromissos (1 a 24).")] int mesesProjecao = InstallmentService.DefaultProjectionMonths,
        CancellationToken cancellationToken = default)
    {
        var purchases = await installments.ListAsync(incluirFinalizados, cancellationToken);
        var commitments = await installments.GetCommitmentsAsync(Math.Clamp(mesesProjecao, 1, 24), cancellationToken);

        return new Parcelamentos(
            [.. purchases.Select(p => new CompraParcelada(
                p.Id, p.CreditCardName, p.Description, p.InstallmentAmount, p.InstallmentCount, p.TotalAmount, p.CurrentNumber, p.RemainingCount,
                p.RemainingAmount, Month(p.FirstInvoiceMonth), Month(p.LastInvoiceMonth)))],
            [.. commitments.Select(c => new Compromisso(Month(c.Month), c.Amount, c.Installments))]);
    }
}