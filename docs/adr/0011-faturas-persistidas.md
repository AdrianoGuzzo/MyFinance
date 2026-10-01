# ADR 0011 — Faturas persistidas com situação e total derivados

- Status: aceita (substitui a [ADR 0005](0005-fatura-de-cartao-calculada.md))
- Data: 2026-10-01

## Contexto

A fatura do MVP era apenas um período calculado: não havia histórico, situação (aberta, fechada, paga, vencida) nem proteção contra "fatura duplicada" ao reimportar. Com o foco em gastos de cartão, a fatura passa a ser a unidade de análise (competência).

## Decisão

- `Invoice` é entidade: cartão, mês de referência (mês de vencimento), início, fechamento, vencimento e `PaidAt`.
- Índice **único** `(CreditCardId, ReferenceMonth)`: uma fatura por cartão e mês; a importação reutiliza a existente (`InvoiceBook`).
- Todo lançamento pertence a uma fatura (`Transaction.InvoiceId` obrigatório), definida na importação (`InvoiceAssigner`).
- **Situação derivada**, não gravada: Aberta antes do fechamento (salvo se marcada como paga); Paga se há `PaidAt` **ou** se os pagamentos importados lançados na fatura seguinte do mesmo cartão cobrem o total (o pagamento acontece depois do fechamento, no período da fatura seguinte) ou o total é zero; Vencida depois do vencimento sem pagamento; senão Fechada.
- **Total calculado** por consulta (soma dos valores de gasto), não gravado.
- Pagamentos importados ("Pagamento recebido") não reduzem o total da fatura em que foram lançados; servem para identificar a quitação da fatura anterior. O usuário também pode marcar a fatura como paga manualmente (ex.: pagamento feito por outro meio ainda não importado).

## Consequências

- O total e a situação nunca ficam desatualizados ao reclassificar, estornar ou importar mais lançamentos.
- Desvio consciente do pedido original (campos `ValorTotal` e `Status` gravados): ambos dependem de outros dados e do dia de hoje.
- Alterar o dia de fechamento do cartão vale para as próximas faturas; as existentes mantêm suas datas.
