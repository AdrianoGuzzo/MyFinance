# ADR 0005 — Fatura de cartão calculada

- Status: substituída pela [ADR 0011](0011-faturas-persistidas.md)
- Data: 2026-09-30

## Contexto

O domínio deve diferenciar conta e cartão e se preparar para `CreditCard → Purchase → Invoice → Payment`. O MVP precisa apenas registrar compras e acompanhar a fatura atual.

## Decisão

- Compras são `Transaction` com `CreditCardId` (nunca saída da conta bancária).
- A fatura é um value object `InvoicePeriod` calculado por `CreditCard.GetInvoicePeriod(data)`:
  - compras em `[fechamento anterior, fechamento)` pertencem à fatura;
  - compras **no** dia de fechamento vão para a fatura seguinte;
  - vencimento no mesmo mês do fechamento se `DueDay > ClosingDay`; senão, no mês seguinte;
  - dias inexistentes (31 em fevereiro) são ajustados para o último dia do mês.
- Não há tabela `Invoice` no MVP.

## Consequências

- "Fatura atual" = soma das **compras** (valores negativos) do cartão no período atual. Pagamentos e estornos não são abatidos no MVP: um pagamento da fatura anterior cai no período aberto e deixaria a fatura atual negativa ([ADR 0009](0009-transferencias-e-regras-do-dashboard.md)).
- Na fase 2, `Invoice` pode virar entidade persistida (status, pagamento, ajustes) reutilizando o mesmo cálculo; `Payment` será uma transferência conta → cartão.
- Regras de fechamento variam por banco; se necessário, a regra "no dia do fechamento" poderá ser configurável por cartão.
