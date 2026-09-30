# ADR 0007 — Armazenamento de decimal no SQLite

- Status: aceita
- Data: 2026-09-30

## Contexto

O SQLite não tem tipo decimal. As opções eram:

1. TEXT (padrão do EF Core);
2. INTEGER em centavos, via value converter;
3. REAL (ponto flutuante) — descartado por imprecisão.

A dúvida era se o EF Core traduz soma, ordenação e comparação de `decimal` armazenado como TEXT.

## Experimento

Teste com valores `0.1, 0.2, -10, 9, 100.55` no EF Core 10.0.12:

- `SumAsync(t => t.Amount)` → `99.85` exato;
- `OrderBy(t => t.Amount)` → `-10, 0.1, 0.2, 9, 100.55` (ordem numérica, não textual);
- `Where(t => t.Amount < 0)` → SQL gerado `WHERE ef_compare("t"."Amount", '0.0') < 0`.

O provider registra funções próprias (`ef_compare`, etc.) que operam com `decimal` do .NET.

## Decisão

Manter `decimal` como TEXT (padrão do EF Core). O teste `Soma_ordenacao_e_comparacao_de_decimal_sao_exatas_no_sqlite` protege esse comportamento em atualizações do EF Core.

## Consequências

- Nenhum conversor extra; o domínio e o banco usam o mesmo tipo.
- Filtros por valor não usam índice (`ef_compare` não é sargável). Aceitável: não filtramos por valor em grandes volumes.
- SQL escrito à mão (fora do EF) **não** deve somar a coluna diretamente: o `SUM` nativo converteria TEXT para REAL. Toda agregação passa pelo EF.
- Se um dia for necessário filtrar por valor com índice, migrar para centavos (INTEGER) é localizado na Infrastructure.
