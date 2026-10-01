# ADR 0011 — Faturas persistidas com situação e total derivados

- Status: aceita (substitui a [ADR 0005](0005-fatura-de-cartao-calculada.md))
- Data: 2026-10-01

## Contexto

A fatura do MVP era apenas um período calculado: não havia histórico, situação (aberta, fechada, paga, vencida) nem proteção contra "fatura duplicada" ao reimportar. Com o foco em gastos de cartão, a fatura passa a ser a unidade de análise (competência).

## Decisão

- `Invoice` é entidade: cartão, mês de referência (mês de vencimento), início, fechamento, vencimento e `PaidAt`.
- Índice **único** `(CreditCardId, ReferenceMonth)`: uma fatura por cartão e mês; a importação reutiliza a existente (`InvoiceBook`).
- Todo lançamento pertence a uma fatura (`Transaction.InvoiceId` obrigatório), definida na importação (`InvoiceAssigner`).
- **Situação derivada**, não gravada: Paga se há `PaidAt`; Aberta antes do fechamento; Vencida depois do vencimento sem pagamento; senão Fechada.
- **Total calculado** por consulta (soma dos valores de gasto), não gravado.
- O pagamento é marcado pelo usuário. Pagamentos importados ("Pagamento recebido") aparecem como informação na fatura em que foram lançados — normalmente a seguinte — e não reduzem o total.

## Consequências

- O total e a situação nunca ficam desatualizados ao reclassificar, estornar ou importar mais lançamentos.
- Desvio consciente do pedido original (campos `ValorTotal` e `Status` gravados): ambos dependem de outros dados e do dia de hoje.
- Alterar o dia de fechamento do cartão vale para as próximas faturas; as existentes mantêm suas datas.
