# MyFinance

Aplicação desktop multiplataforma para gestão financeira pessoal. **Todo o processamento e os dados ficam no seu computador** — sem nuvem, sem telemetria, sem envio de dados financeiros para serviços externos.

> Status: em construção (MVP). Veja [Progresso](#progresso).

## Objetivo

Controlar contas bancárias, cartões de crédito e categorias, importar extratos (OFX e CSV) sem criar lançamentos duplicados, categorizar lançamentos e visualizar saldo, receitas e despesas.

A arquitetura está preparada para evoluir (regras automáticas, IA local para categorização, Open Finance, sincronização, mobile/web) sem reescrever o domínio — veja [docs/architecture.md](docs/architecture.md).

## Requisitos

- [.NET SDK 10.0.401](https://dotnet.microsoft.com/download) ou superior (fixado em `global.json`)
- Windows, Linux ou macOS

## Stack

| Área | Tecnologia |
|---|---|
| Linguagem / runtime | C# / .NET 10 |
| UI | Avalonia UI 12 + CommunityToolkit.Mvvm |
| Persistência | Entity Framework Core 10 + SQLite |
| Logs | Serilog (arquivo com rolling diário) |
| Testes | xUnit v3 (Microsoft.Testing.Platform) + AwesomeAssertions |

Versões centralizadas em [`Directory.Packages.props`](Directory.Packages.props).

## Arquitetura

Camadas com dependências apontando para dentro:

```text
Desktop ──► Application ──► Domain
   │             ▲
   └──► Infrastructure
```

- **Domain** — entidades, value objects, regras (inclusive detecção de duplicidade e períodos de fatura). Sem dependências externas.
- **Application** — casos de uso e contratos (ex.: `ITransactionImporter`).
- **Infrastructure** — EF Core/SQLite, importadores OFX/CSV, logging.
- **Desktop** — Views/ViewModels (MVVM) e composição da aplicação.

Detalhes em [docs/architecture.md](docs/architecture.md).

## Estrutura dos projetos

```text
src/
├── MyFinance.Desktop/          # Avalonia (Views, ViewModels, Assets, Program.cs)
├── MyFinance.Application/      # Casos de uso: Accounts, Transactions, Categories, CreditCards, Imports
├── MyFinance.Domain/           # Entities, ValueObjects, Enums, Interfaces, Services
└── MyFinance.Infrastructure/   # Persistence, Imports (OFX, CSV), Logging
tests/
├── MyFinance.Domain.Tests/
├── MyFinance.Application.Tests/
└── MyFinance.Infrastructure.Tests/
docs/
├── architecture.md
├── database.md
├── import.md
└── adr/
```

## Como executar

```bash
dotnet run --project src/MyFinance.Desktop                                   # perfil Development (launchSettings)
dotnet run --project src/MyFinance.Desktop --launch-profile "MyFinance (Production)"
```

Na primeira execução o banco é criado (migrations) e as categorias padrão são cadastradas.

| | Production (padrão) | Development |
|---|---|---|
| Banco | `%LOCALAPPDATA%\MyFinance\myfinance.db` | `%LOCALAPPDATA%\MyFinance\data\myfinance-dev.db` |
| Logs | `%LOCALAPPDATA%\MyFinance\logs\myfinance-AAAAMMDD.log` | idem, nível Debug |
| Configuração | `appsettings.json` | + `appsettings.Development.json` |

O ambiente vem de `DOTNET_ENVIRONMENT`. Caminhos relativos em `Database:Path`/`Logs:Directory` são resolvidos a partir de `%LOCALAPPDATA%\MyFinance` (no Linux/macOS, a pasta equivalente de dados locais).

Menu: Dashboard · Contas · Cartões · Transações · Categorias · Importar Extrato · Configurações (tema claro/escuro/sistema).

## Como executar os testes

```bash
dotnet test                                      # todos
dotnet test --project tests/MyFinance.Domain.Tests
```

Os testes usam o Microsoft.Testing.Platform (configurado em `global.json`), exigido pelo xUnit v3 no SDK do .NET 10.

## Migrations e banco de dados

A ferramenta `dotnet-ef` está no manifesto local (`dotnet-tools.json`):

```bash
dotnet tool restore

# criar migration
dotnet ef migrations add <Nome> --project src/MyFinance.Infrastructure --output-dir Persistence/Migrations

# atualizar banco
dotnet ef database update --project src/MyFinance.Infrastructure
```

A aplicação aplica migrations pendentes na inicialização (`MigrateAsync`); `EnsureCreated()` não é usado.
O banco fica em `%LOCALAPPDATA%\MyFinance\myfinance.db` (ou equivalente no Linux/macOS). Detalhes em [docs/database.md](docs/database.md).

## Como importar extratos

**OFX** (recomendado — traz identificador único de cada transação):

1. No app/site do banco, exporte o extrato em OFX (no Nubank: *Extrato → Exportar → OFX*).
2. No MyFinance: *Importar Extrato* → selecione o arquivo.
3. Escolha a conta ou cartão de destino (sugerido automaticamente quando o número da conta do arquivo coincide).
4. Revise a prévia (novas, duplicadas, com erro) → *Confirmar*.

> Pagou a fatura do cartão pela conta? Categorize o lançamento como **Transferências > Pagamento de fatura** para que a despesa não seja contada duas vezes (compra no cartão + pagamento).

**CSV**: mesmo fluxo. O arquivo precisa de cabeçalho com colunas de data, valor e descrição; delimitador `,` `;` ou tab; valores em formato brasileiro ou americano. Em faturas de cartão com compras positivas, a prévia sugere inverter os sinais.

Formatos aceitos, colunas reconhecidas e mensagens de erro: [docs/import.md](docs/import.md).

Reimportar o mesmo arquivo ou um período sobreposto não cria duplicados.

## Decisões arquiteturais

Registradas como ADRs em [docs/adr](docs/adr):

| ADR | Decisão |
|---|---|
| [0001](docs/adr/0001-arquitetura-em-camadas.md) | Arquitetura em camadas com domínio isolado |
| [0002](docs/adr/0002-identificadores-guid-v7.md) | Identificadores `Guid` v7 |
| [0003](docs/adr/0003-datas-e-valores-monetarios.md) | `DateOnly` para datas financeiras, `decimal` com sinal para valores |
| [0004](docs/adr/0004-deteccao-de-duplicidades.md) | Detecção de duplicidades em três estratégias com consumo |
| [0005](docs/adr/0005-fatura-de-cartao-calculada.md) | Fatura de cartão calculada a partir dos dias de fechamento/vencimento |
| [0006](docs/adr/0006-microsoft-testing-platform.md) | Microsoft.Testing.Platform para executar testes |
| [0007](docs/adr/0007-decimal-no-sqlite.md) | `decimal` armazenado como TEXT no SQLite (validado por teste) |
| [0008](docs/adr/0008-parsers-proprios-ofx-csv.md) | Parsers próprios de OFX e CSV, sem regras por banco |
| [0009](docs/adr/0009-transferencias-e-regras-do-dashboard.md) | Categorias de transferência e regras de receitas/despesas do dashboard |
| [0010](docs/adr/0010-interface-desktop.md) | Composição e padrões da interface desktop (escopo por operação, diálogos, gráficos nativos) |

## Critérios de conclusão do MVP

| # | Critério | Evidência |
|---|---|---|
| 1 | Abrir a aplicação | Executada em Development: migration aplicada automaticamente, log `ApplicationStarted` |
| 2 | Criar uma conta Nubank | Tela Contas · `AccountServiceTests.CreateAccount_cria_conta_Nubank_com_saldo_calculado` |
| 3 | Criar categorias | Tela Categorias · `CategoryServiceTests` (categoria, subcategoria, nome repetido, desativação) |
| 4 | Selecionar um arquivo OFX | Tela Importar Extrato (seletor nativo via `IStorageProvider`) ¹ |
| 5 | Importar o arquivo | `OfxTransactionImporterTests` · `ImportServiceTests.Importar_OFX_novo_...` |
| 6 | Visualizar uma prévia | Tela de prévia (Data, Descrição, Valor, Status) · nada é gravado antes de confirmar (teste) |
| 7 | Detectar transações duplicadas | `DuplicateDetectorTests` · reimportação, período sobreposto, outro formato, prévias concorrentes |
| 8 | Confirmar a importação | `ImportServiceTests` (confirmação única, recálculo de duplicidades) |
| 9 | Persistir no SQLite | Testes de integração com SQLite real e migrations |
| 10 | Visualizar as transações | Tela Transações (filtros, paginação) — verificada com dados |
| 11 | Categorizar as transações | Categoria direto na linha · `TransactionServiceTests.CategorizeTransaction_...` |
| 12 | Visualizar o saldo | Dashboard e Contas — verificados com dados · `DashboardServiceTests` |
| 13 | Visualizar receitas e despesas | Dashboard (indicadores e gráficos) · `DashboardServiceTests` |
| 14 | Executar todos os testes | `dotnet test` — 207 testes |
| 15 | Rodar sem serviços externos | Nenhum código de rede; fontes embutidas; sem telemetria · `LogPrivacyTests` |

¹ O fluxo de importação pela interface (seleção no diálogo nativo → prévia → confirmar) deve ser validado manualmente; a lógica de cada passo é coberta pelos testes da Application.

## Limitações conhecidas (MVP)

- **Fatura do cartão** soma apenas compras do período aberto; pagamentos e estornos não são abatidos ([ADR 0005](docs/adr/0005-fatura-de-cartao-calculada.md)).
- **Pagamento de fatura** precisa ser categorizado como *Transferências* para não contar a despesa duas vezes ([ADR 0009](docs/adr/0009-transferencias-e-regras-do-dashboard.md)).
- **CSV**: exige cabeçalho reconhecível; não há tela de mapeamento manual de colunas nem colunas separadas de crédito/débito; datas `MM/dd/yyyy` não são aceitas.
- **Categorização automática**: apenas o ponto de extensão (`ICategorizationService`); regras chegam na fase 2.
- **Testes de interface**: ViewModels ainda sem projeto de testes automatizados.

## Progresso

- [x] Etapa 1 — Solução, projetos, dependências e testes configurados
- [x] Etapa 2 — Domínio (entidades, value objects, duplicidade, fatura)
- [x] Etapa 3 — Persistência (EF Core, SQLite, migrations, repositórios)
- [x] Etapa 4 — Importadores OFX e CSV
- [x] Etapa 5 — Casos de uso (Application)
- [x] Etapa 6 — Interface (Avalonia)
- [x] Etapa 7 — Revisão final (privacidade dos logs, revisão de código, critérios do MVP)
