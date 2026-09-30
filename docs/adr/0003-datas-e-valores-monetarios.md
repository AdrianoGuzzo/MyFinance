# ADR 0003 — DateOnly para datas financeiras e decimal com sinal para valores

- Status: aceita
- Data: 2026-09-30

## Contexto

Lançamentos bancários têm data, não horário; `DateTime` introduz problemas de fuso (um lançamento de 30/09 pode "virar" 29/09). Valores monetários exigem precisão exata.

## Decisão

- **Data do lançamento** (`Transaction.Date`, `ImportTransaction.Date`, `ImportedTransaction.Date`): `DateOnly`.
  Isso diverge do esboço inicial de `ImportedTransaction` (que usava `DateTime`) para manter consistência de ponta a ponta.
- **Datas de auditoria** (`CreatedAt`, `UpdatedAt`, `ImportedAt`): `DateTime` em UTC, validado no domínio.
- **Valores**: `decimal`, com no máximo 2 casas decimais — valores com mais casas são rejeitados (sem arredondamento silencioso, que esconderia erros de parsing).
- **Sinal**: positivo = entrada, negativo = saída. `TransactionType` é derivado do sinal. Vale também para cartão: compra é negativa, estorno/pagamento é positivo.

## Consequências

- Somas de saldo e fatura são simples somas de `Amount`.
- Parsers devem converter datas de OFX/CSV para `DateOnly` na data local do extrato.
