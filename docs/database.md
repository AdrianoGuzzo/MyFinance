# Banco de dados

SQLite local acessado via Entity Framework Core 10, com migrations.

## Localização

| Ambiente | Caminho |
|---|---|
| Padrão | `%LOCALAPPDATA%\MyFinance\myfinance.db` (Windows) · `~/.local/share/MyFinance/myfinance.db` (Linux) · `~/Library/Application Support/MyFinance/myfinance.db` (macOS) |
| Configurável | `Database:Path` no `appsettings*.json` ou na linha de comando (vazio = padrão) |
| `dotnet ef` (design-time) | `%TEMP%/myfinance-design.db` — descartável, usado só para gerar/testar migrations |

## Inicialização (`DatabaseInitializer`)

1. **Banco de versão anterior** — se o banco tem migrations aplicadas que esta versão não conhece (o esquema do MVP de contas bancárias), o arquivo é **movido** para `backups/<nome>-legado-AAAAMMDD-HHmmss.db` e um banco novo é criado. Nada é apagado. Evento de log `DatabaseReset`.
2. **Backup antes de migrar** — havendo migrations pendentes em um banco com dados, uma cópia é gravada em `backups/<nome>-pre-migracao-AAAAMMDD-HHmmss.db` (`VACUUM INTO`). Evento `DatabaseBackup`.
3. `MigrateAsync`. `EnsureCreated()` não é usado em nenhum lugar, nem nos testes.

A pasta `backups` fica ao lado do arquivo do banco. O backup manual (*Configurações › Fazer backup*) usa o mesmo `VACUUM INTO` para um arquivo escolhido pelo usuário.

As migrations foram **resetadas** no refoco em cartões (nova `InitialCreate`): não há migração de dados de contas bancárias, que saíram do produto.

## Tabelas

```text
CreditCards ◄─┬── Invoices ◄────────────┐
              ├── InstallmentPurchases ◄─┤
              ├── Imports ◄── ImportTransactions (CASCADE) ──► Transactions (SET NULL)
              └──────────────────────── Transactions ──► Categories ◄── Categories (ParentCategoryId)
                                                             ▲
                       CategoryRules ── SpendingLimits ── RecurringExpenses
FinancialGoals (independente)
```

| Tabela | Observações |
|---|---|
| `CreditCards` | só os 4 últimos dígitos; `Issuer`, `Brand`; checks de dias `BETWEEN 1 AND 31` e `Brand IN (...)` |
| `Invoices` | uma por cartão e mês: índice **único** `(CreditCardId, ReferenceMonth)`; datas de início, fechamento e vencimento; `PaidAt` opcional. Situação e total **não** são gravados ([ADR 0011](adr/0011-faturas-persistidas.md)) |
| `Transactions` | `CreditCardId` e `InvoiceId` obrigatórios; `Kind` (check `IN`); estabelecimento (`MerchantName`, `MerchantKey`); parcela opcional (`InstallmentPurchaseId` + `InstallmentNumber`, check de coerência) |
| `InstallmentPurchases` | compra parcelada: valor da parcela (check > 0), quantidade (2–48), total, mês da 1ª parcela ([ADR 0013](adr/0013-parcelamentos-e-projecao.md)) |
| `Categories` | auto-relacionamento `ParentCategoryId` (um nível); somente categorias de gastos |
| `CategoryRules` | padrão normalizado, categoria, prioridade (check 0–1000), ativa |
| `SpendingLimits` | um limite por categoria (índice **único** `CategoryId`); valor mensal > 0 |
| `FinancialGoals` | meta de economia mensal; uma ativa por vez (regra da aplicação) |
| `RecurringExpenses` | decisão do usuário sobre um recorrente detectado; índice **único** `MerchantKey` |
| `Imports` / `ImportTransactions` | histórico de cada arquivo e de cada registro (tipo e fatura sugeridos inclusive); `RawData` fica só no banco local |

Exclusões: FKs são `RESTRICT` (o fluxo normal é **desativar** cartões e categorias), exceto `ImportTransactions → Imports` (`CASCADE`) e `ImportTransactions.TransactionId` (`SET NULL`). Regras e limites podem ser excluídos: são configuração e nada aponta para eles.

## Tipos

| Conceito | CLR | SQLite |
|---|---|---|
| Identificadores | `Guid` (v7) | TEXT |
| Valores monetários | `decimal` | TEXT — ver [ADR 0007](adr/0007-decimal-no-sqlite.md); checks de positivo usam `CAST(... AS REAL)` apenas na comparação |
| Datas financeiras e meses (`ReferenceMonth`, primeiro dia do mês) | `DateOnly` | TEXT `yyyy-MM-dd` (ordenável) |
| Auditoria (`CreatedAt`, `PaidAt`...) | `DateTime` UTC | TEXT; conversor restaura `DateTimeKind.Utc` na leitura |
| Enums | `enum` | INTEGER, com check `IN` dos valores definidos |
| Value objects | `LastFourDigits`, `HexColor`, `Sha256Hash` → TEXT; `DayOfMonth` → INTEGER | via `ValueConverter` |

## Índices

| Índice | Uso |
|---|---|
| `IX_Transactions_CreditCardId_Date` | filtro por cartão e candidatos a duplicidade por período |
| `IX_Transactions_InvoiceId` | lançamentos e total da fatura; análises por competência (join com `Invoices`) |
| `IX_Transactions_Date` | buscas por data |
| `IX_Transactions_ExternalId`, `IX_Transactions_ImportHash` | duplicidade |
| `IX_Transactions_CategoryId`, `IX_Transactions_MerchantKey` | gastos por categoria e estabelecimento |
| `IX_Transactions_InstallmentPurchaseId` | parcelas já lançadas (projeção) |
| `IX_Invoices_CreditCardId_ReferenceMonth` (único) | fatura do cartão no mês |
| `IX_InstallmentPurchases_CreditCardId_MerchantKey` | vincular parcelas importadas à compra |
| `IX_CategoryRules_IsActive_Priority` | regras ativas por prioridade |
| `IX_Imports_FileHash` | "este arquivo já foi importado" |

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Nome> --project src/MyFinance.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/MyFinance.Infrastructure
```

O teste `Migrations_estao_sincronizadas_com_o_modelo` falha se o modelo mudar sem uma migration correspondente.

## Testes

Os testes de persistência usam SQLite **real** em memória (`DataSource=:memory:`) com as migrations aplicadas — sem provider InMemory do EF, que não respeita FKs, checks nem tradução SQL. Reset de banco legado e backup são testados com arquivos temporários.
