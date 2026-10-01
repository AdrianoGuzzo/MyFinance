# ADR 0012 — Competência da fatura, valor de gasto e tipos de lançamento

- Status: aceita (substitui a [ADR 0009](0009-transferencias-e-regras-do-dashboard.md))
- Data: 2026-10-01

## Contexto

Sem contas bancárias não há receitas, saldo nem transferências entre contas. Mas os arquivos de cartão trazem lançamentos que não são compras (pagamento da fatura, estornos, tarifas, juros), e compras parceladas precisam contar pelo impacto mensal, não pelo valor total.

## Decisão

- **Competência da fatura** em todas as análises (gasto do mês, média, variação, categorias, limites, oportunidades): cada lançamento conta no mês de referência da sua fatura. Uma compra de R$ 6.000 em 12× conta R$ 500 por mês.
- **Mês padrão** ("fatura atual"): a fatura aberta mais próxima do vencimento entre os cartões ativos. O mês pode ser parcial (fatura ainda aberta), e a interface indica isso.
- **Tipo** (`TransactionKind`), sugerido na importação e editável: Compra, Estorno, Pagamento, Tarifa, Juros, Ajuste. O tipo precisa ser coerente com o sinal (a [ADR 0003](0003-datas-e-valores-monetarios.md) continua valendo: negativo = débito no cartão).
- **Valor de gasto** (`Spending.AmountOf`): compra, tarifa e juros somam; estorno subtrai; pagamento da fatura não conta; ajuste segue o sinal.
- **Média**: até 6 meses anteriores ao mês analisado, sem recuar antes do primeiro mês com lançamentos; meses sem gasto dentro desse intervalo contam como zero. Sem histórico, não há média nem variação.
- **Relevância** de uma variação: diferença ≥ R$ 30 e ≥ 10% (gasto novo, sem base, é relevante pela diferença).

## Consequências

- O pagamento de fatura não precisa mais ser categorizado como "transferência" para evitar contagem dupla.
- Gastos do mês corrente aparecem antes de a fatura fechar — o painel compara um mês parcial com meses completos e deixa isso explícito.
- Os limiares ficam em `AnalysisThresholds`, `SavingsOpportunityFinder` e `InsightGenerator`.
