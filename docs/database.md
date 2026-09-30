# Banco de dados

SQLite local acessado via Entity Framework Core 10, com migrations.

## Localização

| Ambiente | Caminho |
|---|---|
| Padrão | `%LOCALAPPDATA%\MyFinance\myfinance.db` (Windows) · `~/.local/share/MyFinance/myfinance.db` (Linux) · `~/Library/Application Support/MyFinance/myfinance.db` (macOS) |
| Configurável | `Database:Path` no `appsettings*.json` (vazio = padrão) |
| `dotnet ef` (design-time) | `%TEMP%/myfinance-design.db` — descartável, usado só para gerar/testar migrations |

Na inicialização, `DatabaseInitializer` aplica as migrations pendentes (`MigrateAsync`). `EnsureCreated()` não é usado em nenhum lugar, nem nos testes.

## Tabelas

```text
Accounts ◄──┐                 ┌──► CreditCards
            │                 │
         Transactions ────────┤
            │   ▲             └──► Categories ◄── Categories (ParentCategoryId)
            │   │
            │   └── ImportTransactions (TransactionId, SET NULL)
            │              │
            │              ▼ (CASCADE)
            └────────── Imports
```

| Tabela | Observações |
|---|---|
| `Accounts` | `AccountNumber` guardado completo (local), exibido sempre mascarado |
| `CreditCards` | só os 4 últimos dígitos; `ClosingDay`/`DueDay` com check `BETWEEN 1 AND 31` |
| `Categories` | auto-relacionamento `ParentCategoryId` |
| `Transactions` | check `CK_Transactions_SingleOwner`: exatamente um entre `AccountId` e `CreditCardId` |
| `Imports` | mesma check de dono único; `FileHash` SHA-256 |
| `ImportTransactions` | histórico de cada registro do arquivo; `RawData` fica só no banco local |

Exclusões: FKs para contas, cartões e categorias são `RESTRICT` — o fluxo normal é **desativar** (`IsActive = false`), não excluir. Apagar uma importação apaga seus itens (`CASCADE`), nunca os lançamentos.

## Tipos

| Conceito | CLR | SQLite |
|---|---|---|
| Identificadores | `Guid` (v7) | TEXT |
| Valores monetários | `decimal` | TEXT — ver [ADR 0007](adr/0007-decimal-no-sqlite.md) |
| Data do lançamento | `DateOnly` | TEXT `yyyy-MM-dd` (ordenável) |
| Auditoria | `DateTime` UTC | TEXT; conversor restaura `DateTimeKind.Utc` na leitura |
| Enums | `enum` | INTEGER |
| Value objects | `AccountNumber`, `LastFourDigits`, `HexColor`, `Sha256Hash` → TEXT; `DayOfMonth` → INTEGER | via `ValueConverter` |

## Índices

| Índice | Uso |
|---|---|
| `IX_Transactions_AccountId_Date` | filtro por conta (prefixo) e busca de candidatos a duplicidade por período |
| `IX_Transactions_CreditCardId_Date` | idem para cartão; fatura por período |
| `IX_Transactions_Date` | relatórios por mês em todas as contas |
| `IX_Transactions_ExternalId` | duplicidade por identificador do banco |
| `IX_Transactions_CategoryId` | despesas por categoria |
| `IX_Transactions_ImportHash` | duplicidade por hash |
| `IX_Imports_FileHash` | "este arquivo já foi importado" |

O índice simples em `Transaction.AccountId` pedido nos requisitos é atendido pelo composto `(AccountId, Date)`: o SQLite usa o prefixo à esquerda, e um índice extra só custaria escrita.

**Índice composto para duplicidade:** avaliado. A busca é "conta + intervalo de datas" (coberta por `(AccountId, Date)`); a comparação fina (valor, descrição normalizada, consumo de pares) acontece em memória no `DuplicateDetector`, porque a descrição normalizada não existe como coluna. Não há índice **único** em `(AccountId, ExternalId)`: alguns bancos reutilizam FITIDs, e uma restrição única faria a importação falhar em vez de mostrar o item na prévia.

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Nome> --project src/MyFinance.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/MyFinance.Infrastructure
```

O teste `Migrations_estao_sincronizadas_com_o_modelo` falha se o modelo mudar sem uma migration correspondente.

## Testes

Os testes de persistência usam SQLite **real** em memória (`DataSource=:memory:`) com as migrations aplicadas — sem provider InMemory do EF, que não respeita FKs, checks nem tradução SQL.
