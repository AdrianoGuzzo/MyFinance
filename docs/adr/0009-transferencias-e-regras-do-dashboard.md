# ADR 0009 — Categorias de transferência e regras de receitas/despesas

- Status: substituída pela [ADR 0012](0012-regras-de-analise-de-gastos.md) (contas bancárias, receitas e transferências saíram do produto)
- Data: 2026-09-30

## Contexto

Com contas e cartões separados, a mesma despesa aparece duas vezes:

1. a compra no cartão (−100, cartão);
2. o pagamento da fatura (−100, conta).

Somar os dois dobra as despesas. O mesmo ocorre com transferências entre contas próprias. O MVP não tem entidade de transferência nem de pagamento de fatura.

## Decisão

- Novo tipo de categoria `CategoryType.Transfer`, com a categoria padrão **Transferências** (*Pagamento de fatura*, *Entre contas*).
- Lançamentos categorizados como transferência **não** contam como receita nem despesa, mas continuam afetando o saldo da conta.
- Regras do dashboard:

| Indicador | Regra |
|---|---|
| Saldo total | Contas **ativas**: saldo inicial + todos os lançamentos. Cartões não entram. |
| Receitas do mês | Valores positivos em **contas**, exceto transferências. |
| Despesas do mês | Valores negativos em contas **e** cartões (pela data da compra), exceto transferências. |
| Fatura atual | Total de **compras** (valores negativos) do período aberto de cada cartão ativo. |
| Despesas por categoria | Despesas do mês agrupadas pela categoria principal; sem categoria → "Sem categoria". |
| Receitas x despesas | Últimos 6 meses, mesmas regras. |
| Evolução do saldo | Saldo das contas ativas no último dia de cada um dos últimos 6 meses. |

- Valores positivos no cartão (pagamentos, estornos) não são receita.

## Consequências

- Sem nova entidade: a correção depende de o usuário categorizar o pagamento da fatura (a fase 2 automatiza com regras — ex.: descrição "Pagamento de fatura" → Transferências).
- Estornos no cartão não abatem despesas nem a fatura atual no MVP. Na fase 2, `Invoice` e `Payment` como entidades (ADR 0005) tratam saldo anterior, pagamentos e estornos.
- A detecção automática de transferência entre contas próprias (mesmo valor, sinais opostos, mesma data) fica como evolução.
