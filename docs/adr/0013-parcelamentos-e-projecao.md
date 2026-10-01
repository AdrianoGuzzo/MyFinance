# ADR 0013 — Parcelamentos reconstruídos a partir das parcelas e projeção

- Status: aceita
- Data: 2026-10-01

## Contexto

Cada fatura traz apenas a parcela do mês ("Notebook - Parcela 3/12"). Para saber quanto das próximas faturas já está comprometido, o app precisa reconhecer a compra parcelada e seu cronograma. O pedido sugeria `InstallmentPurchase` ligada 1:1 a uma transação; na prática há uma transação por parcela.

## Decisão

- `InstallmentPurchase` (cartão, descrição, estabelecimento, valor da parcela, quantidade, total, mês da 1ª parcela) é **1:N** com as transações; cada parcela importada guarda `InstallmentPurchaseId` e `InstallmentNumber`.
- A parcela é reconhecida na descrição (`InstallmentParser`). Na confirmação, `InstallmentMatcher` vincula à compra existente com mesmo cartão, estabelecimento, quantidade e mês da 1ª parcela (valor ± R$ 1,00, por causa do arredondamento da última parcela); se não houver, cria a compra.
- Parcelas com a data **original** da compra são colocadas na fatura do arquivo (`InvoiceAssigner`, [ADR 0011](0011-faturas-persistidas.md)).
- **Projeção** (`CommitmentProjector`): para cada fatura futura, soma as parcelas ainda não lançadas. As próximas faturas no painel = já lançado + parcelas comprometidas.
- "Restante" de uma compra = parcelas em faturas posteriores à fatura atual. Parcelas antigas que nunca foram importadas não contam como pendentes.

## Consequências

- Não é preciso cadastrar compras parceladas à mão: basta importar as faturas.
- Duas compras idênticas no mesmo mês são distinguidas porque uma compra não recebe duas vezes a mesma parcela.
- Descrições como "LOJA 03/12" sem a palavra "parcela" podem ser confundidas com datas `dd/MM`; o padrão sem palavra-chave exige dois dígitos e ainda assim é uma heurística.
