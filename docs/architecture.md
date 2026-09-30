# Arquitetura

## Camadas

```text
┌─────────────────────┐
│ MyFinance.Desktop   │  Avalonia, MVVM, composição (DI, configuração, Serilog)
└──────┬───────┬──────┘
       │       │
       ▼       ▼
┌────────────┐ ┌──────────────────────┐
│ Application│◄┤ Infrastructure       │  EF Core/SQLite, importadores OFX/CSV
└─────┬──────┘ └──────────┬───────────┘
      ▼                   │
┌────────────┐            │
│  Domain    │◄───────────┘
└────────────┘
```

| Camada | Responsabilidade | Pode depender de |
|---|---|---|
| Domain | Entidades, value objects, regras de negócio, serviços de domínio puros | nada (apenas BCL) |
| Application | Casos de uso, contratos de portas (repositórios, importadores), DTOs | Domain, `Microsoft.Extensions.Logging.Abstractions` |
| Infrastructure | Implementações: EF Core, SQLite, parsers OFX/CSV | Application, Domain |
| Desktop | Views, ViewModels, composição | Application, Infrastructure (somente no composition root) |

Regras:

- O domínio **não conhece** SQLite, EF Core, Avalonia, OFX, CSV ou bancos específicos (Nubank é apenas uma fonte de arquivo).
- ViewModels **orquestram** casos de uso; não contêm regra de negócio.
- Importadores convertem o formato externo em `ImportedTransaction` (modelo neutro); a aplicação converte em entidades do domínio.

## Domínio

### Entidades

| Entidade | Observações |
|---|---|
| `Account` | Conta bancária. `AccountNumber` é value object que se mascara em `ToString()`. |
| `CreditCard` | Guarda somente os 4 últimos dígitos. Calcula o `InvoicePeriod` de uma compra. |
| `Category` | Um nível de subcategorias; subcategoria herda o tipo (despesa/receita). |
| `Transaction` | Pertence a **uma** conta **ou** a **um** cartão (`TransactionOwner`). Valor com sinal. |
| `Import` | Agregado da importação de um arquivo; cria os lançamentos ao ser concluído. |
| `ImportTransaction` | Cada registro lido do arquivo e seu destino (novo, duplicado, inválido, importado). |

### Value objects

`TransactionOwner`, `AccountNumber`, `LastFourDigits`, `HexColor`, `DayOfMonth`, `Sha256Hash`, `InvoicePeriod`, `ImportSummary`.

### Serviços de domínio

- `TransactionFingerprint` — normalização de descrição e hash dos dados relevantes.
- `DuplicateDetector` — algoritmo puro de detecção de duplicidade ([ADR 0004](adr/0004-deteccao-de-duplicidades.md)).
- `DefaultCategories` — categorias padrão criadas no primeiro uso.

### Portas de persistência

Interfaces em `Domain/Interfaces` (`IAccountRepository`, `ICreditCardRepository`, `ICategoryRepository`, `ITransactionRepository`, `IImportRepository`, `IUnitOfWork`), implementadas em `Infrastructure/Persistence/Repositories`. Os repositórios trabalham com agregados; consultas de leitura para telas (listagens, dashboard) ficarão em serviços de consulta da Application.

## Casos de uso (Application)

Serviços simples, injetados nos ViewModels (sem Mediator). Recebem comandos (`record`) e devolvem DTOs.

| Serviço | Casos de uso |
|---|---|
| `AccountService` | CreateAccount, UpdateAccount, Deactivate/Activate, listar com saldo |
| `CreditCardService` | CreateCreditCard, Update, Deactivate/Activate, listar com fatura atual |
| `CategoryService` | CreateCategory (inclui subcategoria), Update, Deactivate (cascata nas subcategorias), Activate, listar em árvore, criar categorias padrão |
| `TransactionService` | CategorizeTransaction (atribuir/remover), buscar com filtros e paginação |
| `ImportService` | ImportTransactions: `AnalyzeAsync` → `PreviewAsync` → `ConfirmAsync` ([docs/import.md](import.md)) |
| `DashboardService` | Indicadores e dados dos gráficos ([ADR 0009](adr/0009-transferencias-e-regras-do-dashboard.md)) |

Portas definidas na Application e implementadas na Infrastructure:

- `ITransactionQueries` — leituras sem rastreamento (busca paginada, somatórios, projeções para o dashboard). As **regras** de cálculo ficam na Application; a Infrastructure apenas busca dados.
- `ITransactionImporter` — importadores de arquivo.
- `TimeProvider` (BCL) — relógio injetável; o "mês atual" usa a data local.
- `ICategorizationService` (Domain) — implementação padrão `NoCategorizationService`; registrada com `TryAdd`, pode ser substituída sem alterar casos de uso.

Registro: `services.AddApplication()` e `services.AddInfrastructure(databasePath)`.

## Interface (Desktop)

```text
Program.cs ── Host (DI, appsettings por ambiente, Serilog) ── App ── MainWindow
                                                              │
MainWindowViewModel ── menu ── PageViewModel (Dashboard, Contas, Cartões, Transações,
                                              Categorias, Importar Extrato, Configurações)
                                   │
                                   └─ IUseCaseExecutor ── escopo DI ── serviço da Application
```

| Peça | Papel |
|---|---|
| `PageViewModel.RunAsync` | indicador de ocupado, captura de erros, mensagem amigável |
| `IUseCaseExecutor` | um escopo (e um DbContext) por operação |
| `DialogService` | confirmação/erro/mensagem como sobreposição aguardável |
| `IFilePickerService` | seleção de arquivo via `IStorageProvider` |
| `ImportViewModel` | prévia versionada: trocar destino/sinais invalida a prévia; resultados atrasados são descartados; só confirma a prévia do destino selecionado |
| `ThemeService` | tema claro/escuro/sistema, persistido em `usersettings.json` |
| `Controls/BarChart`, `Controls/LineChart` | gráficos nativos |

Detalhes e justificativas: [ADR 0010](adr/0010-interface-desktop.md).

## Segurança e privacidade

| Garantia | Como |
|---|---|
| Dados só locais | SQLite em `%LOCALAPPDATA%\MyFinance`; nenhum cliente HTTP, telemetria ou analytics no código |
| Logs sem dados financeiros | Mensagens com apenas metadados (extensão, prefixo do hash, contagens, IDs); `EnableSensitiveDataLogging` nunca é usado; EF Core em `Warning` por padrão. **Verificado pelo teste `LogPrivacyTests`**, que captura todos os logs (inclusive SQL do EF) durante importações e procura número de conta, cartão, descrições, valores, nome do arquivo e conteúdo OFX — validado por mutação (um vazamento introduzido de propósito faz o teste falhar) |
| Número da conta | `AccountNumber.ToString()` é mascarado (`****6789`); valor completo só em `.Value` |
| Cartão | somente os 4 últimos dígitos são armazenados |
| Extratos | conteúdo original (`RawData`) fica apenas no banco local, para histórico da importação |

## Tratamento de erros

| Tipo | Classe | Origem |
|---|---|---|
| Validação | `DomainException`, `ValidationException` | regras do domínio / casos de uso |
| Importação | `ImportException` | arquivo ilegível, formato não reconhecido |
| Persistência | `PersistenceException` | `UnitOfWork` converte `DbUpdateException`/`SqliteException` e registra o evento `DatabaseError` |
| Técnico | qualquer outra exceção | registrada como `UnhandledException`; usuário vê mensagem genérica |

Todas as exceções esperadas carregam mensagem amigável em português; detalhes técnicos vão somente para o log.

### Pontos de extensão

- `ICategorizationService` — hoje sem implementação automática; fase 2 (regras) e fase 3 (IA local) implementam esta interface sem mudar o domínio.
- `ITransactionImporter` (Application) — novos formatos ou fontes (ex.: Open Finance na fase 4) produzem `ImportedTransaction` e reutilizam todo o fluxo de prévia/duplicidade/persistência.
- `Guid` v7 como identificador facilita sincronização futura (fase 5) sem conflito de chaves.

## Cartão de crédito

```text
CreditCard ──► Purchase (Transaction do cartão) ──► Invoice (InvoicePeriod) ──► Payment
```

No MVP, compras são `Transaction` com `CreditCardId`, e a fatura é **calculada** ([ADR 0005](adr/0005-fatura-de-cartao-calculada.md)). O pagamento da fatura (transferência conta → cartão) fica para a fase 2.

## Roadmap

```text
FASE 1 Desktop, SQLite, OFX, CSV
FASE 2 Regras automáticas, cartão de crédito completo, relatórios
FASE 3 IA para categorização (local)
FASE 4 Open Finance
FASE 5 API / sincronização
FASE 6 Mobile / Web
```
