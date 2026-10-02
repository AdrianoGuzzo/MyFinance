using System.ComponentModel;

using ModelContextProtocol.Server;

using MyFinance.Application.Transactions;
using MyFinance.Domain.Enums;

using static MyFinance.Mcp.Tools.McpArgs;

namespace MyFinance.Mcp.Tools;

/// <summary>Busca e categorização de lançamentos.</summary>
[McpServerToolType]
internal static class TransacaoTools
{
    public const int MaxPageSize = 200;

    [McpServerTool(Name = "buscar_transacoes", Title = "Buscar lançamentos", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Busca lançamentos do cartão (mais recentes primeiro) com filtros combináveis e paginação.")]
    public static async Task<PaginaTransacoes> BuscarTransacoesAsync(
        TransactionService transactions,
        [Description("Trecho da descrição (sem diferenciar maiúsculas).")] string? texto = null,
        [Description("Id da categoria (inclui as subcategorias).")] Guid? categoriaId = null,
        [Description("Id do cartão.")] Guid? cartaoId = null,
        [Description("Id da fatura.")] Guid? faturaId = null,
        [Description("Primeiro mês de fatura (aaaa-MM).")] string? de = null,
        [Description("Último mês de fatura (aaaa-MM).")] string? ate = null,
        [Description("Tipo: Purchase (compra), Refund (estorno), Payment (pagamento de fatura), Fee (tarifa/IOF), Interest (juros), Adjustment (ajuste).")]
        TransactionKind? tipo = null,
        [Description("Somente lançamentos sem categoria.")] bool somenteSemCategoria = false,
        [Description("Página (a partir de 1).")] int pagina = 1,
        [Description("Itens por página (1 a 200).")] int tamanhoPagina = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await transactions.SearchAsync(new TransactionSearch
        {
            Text = texto,
            CategoryId = categoriaId,
            CreditCardId = cartaoId,
            InvoiceId = faturaId,
            FromMonth = ParseMonth(de, nameof(de)),
            ToMonth = ParseMonth(ate, nameof(ate)),
            Kind = tipo,
            UncategorizedOnly = somenteSemCategoria,
            Page = Math.Max(1, pagina),
            PageSize = Math.Clamp(tamanhoPagina, 1, MaxPageSize),
        }, cancellationToken);

        return new PaginaTransacoes(result.TotalCount, result.Page, result.TotalPages, [.. result.Items.Select(ToDto)]);
    }

    [McpServerTool(Name = "listar_nao_categorizadas", Title = "Lançamentos sem categoria", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Lançamentos sem categoria (pagamentos de fatura ficam de fora). Por padrão agrupa por estabelecimento, com exemplos de "
        + "descrição e os ids, prontos para categorizar_transacoes_em_lote.")]
    public static async Task<Pendentes> ListarNaoCategorizadasAsync(
        TransactionService transactions,
        [Description("Primeiro mês de fatura (aaaa-MM). Vazio = todos.")] string? de = null,
        [Description("Último mês de fatura (aaaa-MM). Vazio = todos.")] string? ate = null,
        [Description("true = agrupa por estabelecimento; false = lista cada lançamento.")] bool agruparPorEstabelecimento = true,
        [Description("Máximo de lançamentos considerados (1 a 500), dos mais recentes para os mais antigos.")] int limite = 300,
        CancellationToken cancellationToken = default)
    {
        var result = await transactions.SearchAsync(new TransactionSearch
        {
            FromMonth = ParseMonth(de, nameof(de)),
            ToMonth = ParseMonth(ate, nameof(ate)),
            UncategorizedOnly = true,
            ExcludePayments = true,
            PageSize = Math.Clamp(limite, 1, TransactionService.MaxBatchSize),
        }, cancellationToken);

        if (!agruparPorEstabelecimento)
        {
            return new Pendentes(result.TotalCount, result.Items.Count, null, [.. result.Items.Select(ToDto)]);
        }

        var groups = result.Items
            .GroupBy(t => t.MerchantName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new GrupoPendente(
                g.Key,
                g.Count(),
                g.Sum(t => t.SpendingAmount),
                [.. g.Select(t => t.Description).Distinct(StringComparer.OrdinalIgnoreCase).Take(3)],
                [.. g.Select(t => t.Id)]))
            .OrderByDescending(g => g.Total)
            .ThenByDescending(g => g.Quantidade)
            .ToList();

        return new Pendentes(result.TotalCount, result.Items.Count, groups, null);
    }

    [McpServerTool(Name = "categorizar_transacao", Title = "Categorizar lançamento", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Atribui (ou remove) a categoria de um lançamento. Pode devolver uma sugestão de regra para categorizar automaticamente "
        + "lançamentos semelhantes (use criar_regra para aceitá-la).")]
    public static async Task<CategorizacaoResultado> CategorizarTransacaoAsync(
        TransactionService transactions,
        DataChangeNotifier notifier,
        [Description("Id do lançamento.")] Guid transacaoId,
        [Description("Id da categoria (veja listar_categorias). Vazio/null remove a categoria.")] Guid? categoriaId = null,
        CancellationToken cancellationToken = default)
    {
        var suggestion = await transactions.CategorizeAsync(transacaoId, categoriaId, cancellationToken);
        notifier.NotifyChanged();
        return new CategorizacaoResultado(true, suggestion is null
            ? null
            : new SugestaoRegra(suggestion.Pattern, suggestion.CategoryId, suggestion.CategoryName, suggestion.MatchingUncategorized));
    }

    [McpServerTool(Name = "categorizar_transacoes_em_lote", Title = "Categorizar lançamentos em lote", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Categoriza vários lançamentos de uma vez (até 500). Tudo ou nada: se algum id não existir ou a categoria estiver "
        + "desativada, nada é alterado e o erro lista os ids com problema.")]
    public static async Task<LoteResultado> CategorizarTransacoesEmLoteAsync(
        TransactionService transactions,
        DataChangeNotifier notifier,
        [Description("Lista de pares lançamento/categoria.")] IReadOnlyList<ItemCategorizacao> itens,
        CancellationToken cancellationToken = default)
    {
        var result = await transactions.CategorizeManyAsync(
            [.. (itens ?? []).Select(i => new CategorizationItem(i.TransacaoId, i.CategoriaId))], cancellationToken);
        notifier.NotifyChanged();
        return new LoteResultado(result.Categorized, result.Uncategorized);
    }

    internal static Transacao ToDto(TransactionListItem t) => new(
        t.Id,
        Date(t.Date),
        Month(t.InvoiceMonth),
        t.Description,
        t.MerchantName,
        t.Amount,
        t.SpendingAmount,
        t.Kind,
        t.CreditCardName,
        t.CategoryId,
        t.CategoryName,
        t.InstallmentNumber is { } n && t.InstallmentCount is { } c ? $"{n}/{c}" : null);
}