using MyFinance.Domain.Analysis;
using MyFinance.Domain.Enums;

namespace MyFinance.Mcp.Tools;

// Resultados compactos para a IA: meses como "aaaa-MM", datas como "aaaa-MM-dd", valores em reais.

internal sealed record OkResult(bool Ok = true);

internal sealed record IdResult(Guid Id);

internal sealed record CategoriaValor(Guid? CategoriaId, string Nome, decimal Valor, decimal Percentual, decimal? VsMesAnterior, decimal? VsMedia);

internal sealed record Variacao(Guid? CategoriaId, string Nome, decimal Atual, decimal Base, decimal Diferenca, decimal? Percentual);

internal sealed record MesValor(string Mes, decimal Valor);

internal sealed record FaturaAberta(Guid CartaoId, string Cartao, string Mes, string Fechamento, string Vencimento, decimal Valor);

internal sealed record ProximaFatura(string Mes, decimal Lancado, decimal Parcelas, decimal Total);

internal sealed record Oportunidade(string Titulo, string Detalhe, decimal PotencialMensal);

internal sealed record MetaProgresso(string Nome, decimal ValorMensal, decimal PotencialIdentificado);

internal sealed record Insight(InsightTone Tom, string Mensagem);

internal sealed record ResumoMes(
    string Mes,
    bool Parcial,
    decimal GastoMes,
    decimal? Media,
    decimal? Variacao,
    int MesesHistorico,
    decimal TotalFaturasAbertas,
    IReadOnlyList<FaturaAberta> FaturasAbertas,
    IReadOnlyList<ProximaFatura> ProximasFaturas,
    IReadOnlyList<CategoriaValor> Categorias,
    IReadOnlyList<Variacao> AumentosVsMesAnterior,
    IReadOnlyList<Variacao> ReducoesVsMesAnterior,
    IReadOnlyList<Variacao> AumentosVsMedia,
    IReadOnlyList<Variacao> ReducoesVsMedia,
    IReadOnlyList<MesValor> Evolucao,
    decimal ParcelasAFaturar,
    IReadOnlyList<Oportunidade> Oportunidades,
    MetaProgresso? Meta,
    IReadOnlyList<Insight> Insights);

internal sealed record CategoriaRelatorio(Guid? CategoriaId, string Nome, decimal Total, decimal Percentual, decimal MediaMensal, decimal? Variacao);

internal sealed record EstabelecimentoRelatorio(string Estabelecimento, int Compras, decimal Total, decimal Media);

internal sealed record MesRelatorio(string Mes, decimal Total, decimal? Media, decimal? Variacao);

internal sealed record Periodo<T>(string De, string Ate, IReadOnlyList<T> Itens);

internal sealed record FaturaAtual(string Mes, string Fechamento, string Vencimento, decimal Valor);

internal sealed record Cartao(
    Guid Id, string Nome, string Emissor, CardBrand Bandeira, decimal Limite, int DiaFechamento, int DiaVencimento, bool Ativo,
    FaturaAtual FaturaAtual, decimal? UsoLimite);

internal sealed record Fatura(
    Guid Id, Guid CartaoId, string Cartao, string Mes, string Inicio, string Fechamento, string Vencimento, InvoiceStatus Situacao,
    decimal Total, decimal Pagamentos, int Lancamentos);

internal sealed record CompraParcelada(
    Guid Id, string Cartao, string Descricao, decimal ValorParcela, int Parcelas, decimal Total, int? ParcelaAtual, int Restantes,
    decimal ValorRestante, string PrimeiraFatura, string UltimaFatura);

internal sealed record Compromisso(string Mes, decimal Valor, int Parcelas);

internal sealed record Parcelamentos(IReadOnlyList<CompraParcelada> Compras, IReadOnlyList<Compromisso> Compromissos);

/// <param name="Valor">Valor no cartão: negativo = débito (compra, tarifa, juros), positivo = crédito (estorno, pagamento).</param>
/// <param name="Gasto">Quanto conta como gasto: positivo para compras, negativo para estornos, zero para pagamentos.</param>
internal sealed record Transacao(
    Guid Id, string Data, string MesFatura, string Descricao, string Estabelecimento, decimal Valor, decimal Gasto, TransactionKind Tipo,
    string Cartao, Guid? CategoriaId, string? Categoria, string? Parcela);

internal sealed record PaginaTransacoes(int Total, int Pagina, int TotalPaginas, IReadOnlyList<Transacao> Itens);

internal sealed record GrupoPendente(string Estabelecimento, int Quantidade, decimal Total, IReadOnlyList<string> Exemplos, IReadOnlyList<Guid> TransacaoIds);

/// <param name="TotalPendentes">Total de lançamentos sem categoria no período (pode ser maior que os listados).</param>
internal sealed record Pendentes(int TotalPendentes, int Listados, IReadOnlyList<GrupoPendente>? Grupos, IReadOnlyList<Transacao>? Itens);

internal sealed record SugestaoRegra(string Padrao, Guid CategoriaId, string Categoria, int PendentesQueCasariam);

internal sealed record CategorizacaoResultado(bool Ok, SugestaoRegra? SugestaoRegra);

internal sealed record LoteResultado(int Categorizadas, int SemCategoria);

internal sealed record AplicacaoRegras(int Categorizadas);

internal sealed record Categoria(Guid Id, string Nome, string NomeCompleto, Guid? CategoriaPaiId, bool Ativa);

internal sealed record CategoriaCriada(Guid Id, string NomeCompleto);

internal sealed record Regra(Guid Id, string Padrao, Guid CategoriaId, string Categoria, int Prioridade, bool Ativa);

internal sealed record Recorrente(
    Guid Id, string Nome, decimal ValorMensal, decimal ValorAnual, string? Categoria, RecurringClassification Classificacao, bool Descartado,
    string UltimoMes, int MesesVistos);

internal sealed record Recorrentes(IReadOnlyList<Recorrente> Itens, decimal TotalMensal, decimal TotalAnual, decimal OpcionalMensal);

internal sealed record Meta(Guid Id, string Nome, decimal ValorMensal, decimal ValorAnual);

internal sealed record PassoOportunidade(
    OpportunitySource Origem, Guid? CategoriaId, string Titulo, string Detalhe, decimal PotencialMensal, decimal Acumulado, bool AtingeMeta);

internal sealed record BaseCategoria(Guid? CategoriaId, string Nome, decimal ValorMensal);

internal sealed record Estrategia(
    string MesReferencia, Meta? Meta, IReadOnlyList<PassoOportunidade> Oportunidades, decimal PotencialIdentificado, decimal? FaltaParaMeta,
    int MesesHistorico, IReadOnlyList<BaseCategoria> BasesPorCategoria);

internal sealed record LinhaCenario(Guid? CategoriaId, string Nome, decimal ValorMensal, decimal Reducao, decimal EconomiaMensal);

internal sealed record Cenario(
    decimal GastoMensalAtual, decimal EconomiaMensal, decimal EconomiaAnual, decimal GastoMensalProjetado, IReadOnlyList<LinhaCenario> Linhas);

internal sealed record Limite(
    Guid Id, Guid CategoriaId, string Categoria, decimal ValorMensal, bool Ativo, decimal Usado, decimal Disponivel, decimal Excesso,
    decimal Percentual, LimitStatus Situacao);

// Entradas compostas.

internal sealed record ItemCategorizacao(
    [property: System.ComponentModel.Description("Id do lançamento.")] Guid TransacaoId,
    [property: System.ComponentModel.Description("Id da categoria; null remove a categoria.")] Guid? CategoriaId);

internal sealed record ReducaoCategoria(
    [property: System.ComponentModel.Description("Id da categoria principal (veja basesPorCategoria em obter_estrategia).")] Guid CategoriaId,
    [property: System.ComponentModel.Description("Redução entre 0 e 1 (0.15 = 15%).")] decimal Percentual);