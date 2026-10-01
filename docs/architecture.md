# Arquitetura

O MyFinance é uma ferramenta de **análise e estratégia de gastos de cartão de crédito**. O objeto central é o gasto de cartão:

```text
Cartão ──► Fatura ──► Transação ──► Categoria ──► Subcategoria ──► Análise
Compra parcelada ──► Parcelas ──► Faturas futuras
```

Saldo bancário, conta corrente, transferências, investimentos e contas a pagar/receber estão fora do escopo (ver [análise do refoco](analise-refoco-cartao.md)).

## Camadas

```text
┌─────────────────────┐
│ MyFinance.Desktop   │  Avalonia, MVVM, composição (DI, configuração, Serilog)
└──────┬───────┬──────┘
       │       │
       ▼       ▼
┌────────────┐ ┌──────────────────────┐
│ Application│◄┤ Infrastructure       │  EF Core/SQLite, importadores OFX/CSV, backup
└─────┬──────┘ └──────────┬───────────┘
      ▼                   │
┌────────────┐            │
│  Domain    │◄───────────┘
└────────────┘
```

| Camada | Responsabilidade | Pode depender de |
|---|---|---|
| Domain | Entidades, value objects, regras de negócio e cálculos de análise puros | nada (apenas BCL) |
| Application | Casos de uso, portas (repositórios, consultas, importadores, backup), DTOs | Domain, `Microsoft.Extensions.Logging.Abstractions` |
| Infrastructure | Implementações: EF Core, SQLite, parsers OFX/CSV, backup | Application, Domain |
| Desktop | Views, ViewModels, composição | Application, Infrastructure (somente no composition root) |

Regras:

- O domínio **não conhece** SQLite, EF Core, Avalonia, OFX, CSV ou bancos específicos.
- ViewModels **orquestram** casos de uso; não contêm regra de negócio.
- Importadores convertem o formato externo em `ImportedTransaction` (modelo neutro).
- Cálculos de análise ficam em `Domain/Analysis` (funções puras sobre `SpendingEntry`); a Infrastructure apenas busca os dados (`ISpendingQueries`).

## Domínio

### Entidades

| Entidade | Observações |
|---|---|
| `CreditCard` | Instituição, bandeira, 4 últimos dígitos, limite, fechamento e vencimento. `GetInvoicePeriod(data)` e `GetInvoicePeriodForMonth(mês)` |
| `Invoice` | Fatura de um cartão em um mês de referência (mês de vencimento). Situação derivada (`GetStatus(hoje)`), total calculado ([ADR 0011](adr/0011-faturas-persistidas.md)) |
| `Transaction` | Lançamento de uma fatura: valor com sinal, `Kind` (compra, estorno, pagamento, tarifa, juros, ajuste), estabelecimento normalizado, categoria, parcela. `SpendingAmount` = quanto conta como gasto ([ADR 0012](adr/0012-regras-de-analise-de-gastos.md)) |
| `InstallmentPurchase` | Compra parcelada: cronograma, parcelas e valor restantes ([ADR 0013](adr/0013-parcelamentos-e-projecao.md)) |
| `Category` | Categoria de gastos com um nível de subcategorias |
| `CategoryRule` | "Descrição contém PADRÃO (início de palavra)" → categoria, com prioridade |
| `SpendingLimit` | Limite mensal por categoria; `Evaluate(usado)` → dentro (< 80%), próximo, excedido |
| `FinancialGoal` | Objetivo de economia mensal |
| `RecurringExpense` | Decisão do usuário sobre um gasto recorrente detectado (classificação, descarte) |
| `Import` / `ImportTransaction` | Agregado da importação de um arquivo e seus itens |

Value objects: `LastFourDigits`, `DayOfMonth`, `HexColor`, `Sha256Hash`, `InvoicePeriod`, `ImportSummary`.

### Serviços de domínio (`Domain/Services`)

| Serviço | Papel |
|---|---|
| `DuplicateDetector`, `TransactionFingerprint` | Detecção de duplicidades ([ADR 0004](adr/0004-deteccao-de-duplicidades.md)) |
| `InvoiceAssigner` | Fatura de cada lançamento importado (inclusive parcelas com a data original) |
| `InstallmentParser`, `InstallmentMatcher` | Parcela na descrição e vínculo com a compra parcelada |
| `MerchantNormalizer` | Estabelecimento a partir da descrição |
| `TransactionKindClassifier` | Tipo pelo sinal e palavras da descrição |
| `CategoryRuleMatcher` | Regra vencedora (prioridade, padrão mais longo, mais antiga) |
| `Spending` | Regra única do valor de gasto |
| `DefaultCategories` | Categorias e regras criadas no primeiro uso |

### Análises (`Domain/Analysis`)

| Cálculo | Papel |
|---|---|
| `SpendingCalculator` | Gasto do mês, série mensal, média (até 6 meses anteriores, a partir do 1º mês com dados), variação, distribuição por categoria |
| `VariationAnalyzer` | "O que aumentou / diminuiu" contra o mês anterior ou a média (limiar: R$ 30 e 10%) |
| `CommitmentProjector` | Parcelas ainda não lançadas por fatura futura |
| `InsightGenerator` | Frases baseadas exclusivamente nos dados (comparações com a média exigem 3 meses de histórico) |
| `RecurringExpenseDetector` | Mesmo estabelecimento em ≥ 3 dos últimos 6 meses, ~1×/mês, valor ± 20% da mediana |
| `SavingsOpportunityFinder` | Categoria > 115% da média (e ≥ R$ 50), limite excedido, recorrentes Opcional/Avaliar |
| `ScenarioSimulator`, `GoalPlanner` | Simulação de reduções e comparação da meta com as oportunidades |

## Casos de uso (Application)

Serviços simples, injetados nos ViewModels (sem Mediator). Recebem comandos (`record`) e devolvem DTOs.

| Serviço | Casos de uso |
|---|---|
| `CreditCardService` | Cadastro de cartões; fatura aberta e uso do limite |
| `InvoiceService` | Faturas com total e situação; marcar/desmarcar paga |
| `TransactionService` | Busca com filtros (cartão, fatura, competência, categoria, tipo, texto); categorizar (com sugestão de regra); alterar tipo |
| `CategoryService`, `CategoryRuleService` | Categorias e regras; aplicar regras aos lançamentos sem categoria |
| `ImportService` | `AnalyzeAsync` → `PreviewAsync` → `ConfirmAsync` ([docs/import.md](import.md)) |
| `DashboardService` | Visão do mês (fatura atual por padrão) |
| `InstallmentService` | Compras parceladas e comprometimento futuro |
| `ReportService` | Relatórios por categoria, estabelecimento e evolução |
| `SpendingLimitService`, `RecurringExpenseService`, `StrategyService` | Limites, recorrentes, meta, oportunidades e cenários |

Componentes compartilhados: `AnalysisLoader` (mês de referência padrão e dados de um intervalo), `SavingsAnalysis` (limites, recorrentes e oportunidades do mês) e `InvoiceBook` (fatura do cartão por mês, criada quando falta).

Portas implementadas na Infrastructure: repositórios (`Domain/Interfaces`), `ITransactionQueries` (telas), `ISpendingQueries` (análises por competência), `ITransactionImporter`, `IDatabaseBackup`. `ICategorizationService` (Domain) é implementado por `RuleBasedCategorizationService` e pode ser substituído (ex.: IA local) sem alterar os casos de uso. `TimeProvider` é o relógio injetável; o "hoje" usa a data local.

## Interface (Desktop)

```text
Program.cs ── Host (DI, appsettings por ambiente, Serilog) ── App ── MainWindow
                                                              │
MainWindowViewModel ── menu ── PageViewModel (Dashboard, Gastos, Faturas, Cartões, Parcelamentos,
                                              Categorias, Limites, Recorrentes, Estratégia,
                                              Relatórios, Importação, Configurações)
                                   │
                                   └─ IUseCaseExecutor ── escopo DI ── serviço da Application
```

| Peça | Papel |
|---|---|
| `PageViewModel.RunAsync` | indicador de ocupado, captura de erros, mensagem amigável |
| `IUseCaseExecutor` | um escopo (e um DbContext) por operação |
| `DialogService` | confirmação/erro/mensagem como sobreposição aguardável (inclui "Criar regra?") |
| `IFilePickerService` | abrir extrato e escolher o destino do backup via `IStorageProvider` |
| `ImportViewModel` | prévia versionada: trocar cartão, sinais ou fatura invalida a prévia; resultados atrasados são descartados |
| `ThemeService` | tema claro/escuro/sistema, persistido em `usersettings.json` |
| `Controls/ColumnChart` | colunas nativas (série única ou empilhada), dica ao passar o mouse; paleta validada para daltonismo nos dois temas (`MfSeries1Brush`, `MfSeries2Brush`) |

Para adicionar uma tela: ViewModel (`PageViewModel`), View, `DataTemplate` em `App.axaml`, `AddSingleton` em `Desktop/DependencyInjection.cs` e entrada em `MainWindowViewModel.Navigation`. Detalhes: [ADR 0010](adr/0010-interface-desktop.md).

## Segurança e privacidade

| Garantia | Como |
|---|---|
| Dados só locais | SQLite em `%LOCALAPPDATA%\MyFinance`; nenhum cliente HTTP, telemetria ou analytics no código |
| Logs sem dados financeiros | Apenas metadados (extensão, prefixo do hash, contagens, IDs, nome do arquivo de backup); `EnableSensitiveDataLogging` nunca é usado. **Verificado pelo `LogPrivacyTests`** |
| Cartão | somente os 4 últimos dígitos são armazenados |
| Extratos | conteúdo original (`RawData`) fica apenas no banco local |
| Backup | local, em arquivo escolhido pelo usuário; cópia automática antes de migrar ou ao encontrar banco de versão anterior |
| Criptografia | não nesta versão ([ADR 0015](adr/0015-privacidade-backup-e-criptografia.md)) |

## Tratamento de erros

| Tipo | Classe | Origem |
|---|---|---|
| Validação | `DomainException`, `ValidationException` | regras do domínio / casos de uso |
| Importação | `ImportException` | arquivo ilegível, formato não reconhecido, extrato de conta bancária |
| Persistência | `PersistenceException` | `UnitOfWork` e backup convertem falhas de banco/arquivo e registram `DatabaseError` |
| Técnico | qualquer outra exceção | registrada como `UnhandledException`; usuário vê mensagem genérica |

Todas as exceções esperadas carregam mensagem amigável em português; detalhes técnicos vão somente para o log.

## Pontos de extensão

- `ICategorizationService` — regras hoje; IA local depois, sem mudar o domínio. Se houver IA, os dados devem passar por agregação/anonimização antes ([ADR 0015](adr/0015-privacidade-backup-e-criptografia.md)).
- `ITransactionImporter` — novos formatos (PDF) ou fontes produzem `ImportedTransaction` e reutilizam prévia, fatura, parcelas, duplicidade e persistência.
- `Guid` v7 facilita uma sincronização futura sem conflito de chaves.
