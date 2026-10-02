using System.ComponentModel;

using ModelContextProtocol.Server;

namespace MyFinance.Mcp.Prompts;

/// <summary>Roteiros prontos para as tarefas mais comuns.</summary>
[McpServerPromptType]
internal static class MyFinancePrompts
{
    [McpServerPrompt(Name = "categorizar_pendentes", Title = "Categorizar lançamentos pendentes")]
    [Description("Roteiro para categorizar os lançamentos sem categoria, com confirmação antes de gravar.")]
    public static string CategorizarPendentes() => """
        Quero organizar meus gastos de cartão no MyFinance.
        1. Use listar_categorias e listar_regras para conhecer as categorias e as regras existentes.
        2. Use listar_nao_categorizadas (agrupado por estabelecimento).
        3. Para cada estabelecimento, proponha a categoria mais adequada (use nomeCompleto). Se nenhuma servir, sugira criar uma
           com criar_categoria. Mostre a proposta em uma tabela (estabelecimento, quantidade, total, categoria) e pergunte antes de gravar.
        4. Depois da minha confirmação, aplique com categorizar_transacoes_em_lote.
        5. Para estabelecimentos que se repetem, sugira regras (criar_regra) e, se eu aceitar, rode aplicar_regras_pendentes.
        Ao final, resuma quantos lançamentos foram categorizados e quantos ainda estão pendentes.
        """;

    [McpServerPrompt(Name = "analisar_mes", Title = "Analisar os gastos do mês")]
    [Description("Roteiro para analisar os gastos de um mês de fatura e apontar oportunidades de economia.")]
    public static string AnalisarMes([Description("Mês da fatura (aaaa-MM). Vazio = fatura atual.")] string? mes = null)
    {
        var month = string.IsNullOrWhiteSpace(mes) ? "da fatura atual" : $"de {mes.Trim()}";
        return $"""
            Analise meus gastos de cartão {month} no MyFinance.
            Use resumo_mes{(string.IsNullOrWhiteSpace(mes) ? string.Empty : $" (mes = {mes.Trim()})")}, gastos_por_categoria,
            gastos_por_estabelecimento, listar_limites, listar_recorrentes e obter_estrategia.
            Explique: quanto gastei e como isso se compara à média; quais categorias e estabelecimentos mais pesaram; o que aumentou ou
            diminuiu; parcelas que ainda vão cair; recorrentes que poderiam ser revistos; e oportunidades de economia com valores.
            Se houver muitos lançamentos sem categoria, avise que a análise por categoria fica imprecisa.
            Trate as oportunidades como informação, não como obrigação. Valores em reais (BRL).
            """;
    }
}