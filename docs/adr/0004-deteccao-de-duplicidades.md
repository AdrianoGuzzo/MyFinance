# ADR 0004 — Detecção de duplicidades em três estratégias com consumo

- Status: aceita
- Data: 2026-09-30

## Contexto

Reimportar um extrato (ou um período sobreposto, ou o mesmo período em outro formato) não pode duplicar lançamentos. Nem todo formato fornece identificador (CSV geralmente não). Compras idênticas legítimas acontecem (dois cafés no mesmo dia).

## Decisão

Serviço de domínio puro `DuplicateDetector`, aplicado por conta/cartão, com estratégias em ordem de prioridade: `ExternalId` → `ImportHash` → `SameData` (data + valor + descrição normalizada).

- Cada lançamento existente é **consumido** por no máximo uma candidata, resolvendo compras idênticas.
- Estratégias de maior prioridade rodam para todas as candidatas antes das de menor, para que um casamento por dados não "roube" o lançamento que casaria por `ExternalId`.
- `ExternalId`s diferentes impedem casamento pelas demais estratégias.
- **Identificador ambíguo** (revisão da etapa 7): alguns bancos reutilizam FITIDs. Se o identificador é **único** no arquivo e no banco, basta o valor coincidir (o banco pode ter corrigido data ou descrição). Se está **repetido** no arquivo ou no banco, exige também data e descrição iguais. Sem essa regra, um banco que usa sempre o mesmo FITID teria todas as transações novas descartadas como duplicadas.
- Casamentos por `ExternalId` também consomem o lançamento existente.
- O hash do **arquivo** (SHA-256 em `Import.FileHash`) é uma verificação adicional apenas informativa: o usuário é avisado mas pode revisar.

A Application carrega do banco somente os lançamentos da conta/cartão no intervalo de datas do arquivo (índice `(AccountId, Date)`) e entrega ao detector.

## Consequências

- Algoritmo 100% testável em memória.
- `ImportHash` é persistido em `Transaction` (coluna indexada).
- Falsos positivos possíveis na estratégia 3 (duas compras idênticas vindas de formatos diferentes sem identificador); a prévia mostra o status e o motivo para o usuário revisar.

> Nota (2026-10-01): após o refoco em cartões, a detecção é feita por **cartão**, com o índice `(CreditCardId, Date)`. O algoritmo não mudou.
