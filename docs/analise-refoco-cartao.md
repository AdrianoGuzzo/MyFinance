# Análise — refoco do MyFinance em gastos de cartão de crédito

- Data: 2026-09-30
- Objetivo da mudança: deixar de ser um controle de **saldo bancário** e passar a ser uma ferramenta de **análise e estratégia de gastos de cartão** ("para onde meu dinheiro está indo e onde posso reduzir?").

Este documento é o resultado da Fase 1 (análise do código existente, antes de qualquer alteração).

## 1. Arquitetura atual

```text
Desktop ──► Application ──► Domain
   │             ▲
   └──► Infrastructure
```

| Camada | Conteúdo |
|---|---|
| Domain | Entidades, value objects, `DuplicateDetector`, `TransactionFingerprint`, `DefaultCategories`, portas de repositório. Sem dependências. |
| Application | Serviços de caso de uso (`AccountService`, `CreditCardService`, `CategoryService`, `TransactionService`, `ImportService`, `DashboardService`), portas `ITransactionQueries` e `ITransactionImporter`. |
| Infrastructure | EF Core + SQLite (configurações, repositórios, `TransactionQueries`, migrations), importadores OFX/CSV próprios, Serilog. |
| Desktop | Avalonia 12 + CommunityToolkit.Mvvm. ViewModels singletons; `IUseCaseExecutor` abre um escopo de DI (e um `DbContext`) por operação; `PageViewModel.RunAsync` centraliza erros; diálogos como sobreposição; gráficos nativos (`BarChart` em pares, `LineChart`). |

Uma página nova exige: ViewModel (`PageViewModel`), View (`.axaml` + code-behind mínimo), `DataTemplate` em `App.axaml`, `AddSingleton` em `Desktop/DependencyInjection.cs` e uma entrada em `MainWindowViewModel.Navigation`.

## 2. Entidades atuais

| Entidade | Observações |
|---|---|
| `Account` | Conta bancária com `InitialBalance` e `BalanceWith(total)`. |
| `CreditCard` | 4 últimos dígitos, limite, dias de fechamento/vencimento; `GetInvoicePeriod(data)` calcula a fatura. |
| `Category` | Um nível de subcategorias; `CategoryType` Expense / Income / Transfer. |
| `Transaction` | Pertence a **uma conta ou um cartão** (`TransactionOwner`); valor com sinal; `TransactionType` Income/Expense derivado do sinal. |
| `Import` / `ImportTransaction` | Agregado da importação de um arquivo e seus itens (novo, duplicado, inválido, importado). |

Value objects: `AccountNumber`, `LastFourDigits`, `DayOfMonth`, `HexColor`, `Sha256Hash`, `InvoicePeriod`, `TransactionOwner`, `ImportSummary`.

## 3. Fluxos existentes

- **Importação**: `AnalyzeAsync` (formato, leitura, SHA-256, sugestão de conta/cartão) → `PreviewAsync` (validação pelo domínio, duplicidade em 3 estratégias, inversão de sinal sugerida para CSV de cartão) → `ConfirmAsync` (recalcula duplicidades e grava tudo num único `SaveChanges`).
- **Categorização**: manual, por lançamento; `ICategorizationService` existe mas a implementação (`NoCategorizationService`) não sugere nada.
- **Dashboard**: saldo total das contas, receitas/despesas do mês, fatura atual, despesas por categoria, receitas × despesas (6 meses) e evolução do saldo.

## 4. Funcionalidades ligadas a saldo bancário (saem do produto)

| Camada | Itens |
|---|---|
| Domain | `Account`, `AccountNumber`, `AccountType`, `TransactionOwner`/`TransactionOwnerType`, `CategoryType.Income`/`Transfer`, categorias padrão "Salário", "Investimentos", "Transferências" |
| Application | `AccountService`; `ITransactionQueries.GetAccountTotalsAsync`; `DashboardDto.TotalBalance/MonthIncome/MonthBalance/IncomeVsExpenses/BalanceEvolution`; sugestão de conta pelo ACCTID em `ImportService` |
| Infrastructure | `AccountRepository`, `AccountConfiguration`, `AccountNumberConverter`, checks `CK_Transactions_SingleOwner`/`CK_Imports_SingleOwner`, índice `IX_Transactions_AccountId_Date` |
| Desktop | `AccountsViewModel`/`AccountsView`, `Options.AccountTypes`, `OwnerOption`, opções "Conta ·" em Transações e Importação, KPIs e gráficos de saldo/receitas |
| Docs | ADR 0005 (fatura calculada, sem entidade) e ADR 0009 (transferências para evitar contagem dupla) |

## 5. Reaproveitável

- `DuplicateDetector` e `TransactionFingerprint` (independentes de conta/cartão).
- `CreditCard.GetInvoicePeriod` / `InvoicePeriod` — base das faturas persistidas.
- Importadores OFX/CSV e utilitários (`CsvReader`, `CsvColumnMap`, `AmountParser`, `DateParser`, `ImportText`) — já suportam extrato de cartão (`CCSTMTRS`) e CSV com compras positivas.
- Fluxo Analyze → Preview → Confirm e a inversão de sinal de CSV de cartão.
- `ICategorizationService` como ponto de extensão para regras.
- `Guard`, exceções amigáveis, `PageViewModel.RunAsync`, `DialogService`, `UseCaseExecutor`, `ChartBase`.
- Infraestrutura de testes: `TestHost` (SQLite real em memória com migrations), `SqliteTestDatabase`, `LogPrivacyTests`.

## 6. Dependências e pontos de acoplamento

- `TransactionOwner` atravessa todas as camadas (domínio, importação, consultas, ViewModels).
- `DashboardService` e `ImportService` dependem de `IAccountRepository`.
- `CreditCardService.GetInvoiceAsync` é `internal static` e reutilizado pelo `DashboardService` (acoplamento entre serviços).
- `ITransactionQueries` mistura consultas de tela (busca paginada) com agregações de saldo.
- ViewModels de Transações e Importação montam listas "conta ou cartão".

## 7. Problemas encontrados

1. **Fatura não é entidade**: total soma só compras (estornos ignorados), não há status, histórico, pagamento nem proteção contra "fatura duplicada".
2. **Parcelamento inexistente**: "Parcela 3/12" é um lançamento isolado; não há projeção de comprometimento futuro.
3. **Tipo derivado apenas do sinal**: pagamento de fatura e estorno viram "receita".
4. **Contagem dupla** depende de o usuário categorizar o pagamento da fatura como Transferência.
5. **N+1** em `ImportService.ApplySuggestedCategoriesAsync` (uma consulta de categoria por lançamento).
6. **Sem estabelecimento normalizado**: não é possível agrupar por loja nem detectar gastos recorrentes.
7. **Desktop sem testes automatizados**.
8. `BarChart` só desenha séries em pares.

## 8. Testes afetados

A maioria dos testes usa os helpers `ApplicationTestBase.CreateAccountAsync` e `TestData.AccountOwner`; eles passam a criar um cartão. Precisam ser reescritos (o significado é de conta/saldo):

- `AccountTests`, `AccountServiceTests`, `DashboardServiceTests` (todos);
- `ImportServiceTests` que dependem de ACCTID de conta, `Conta_bancaria_nunca_sugere_inversao`, `Duplicidade_e_verificada_somente_na_mesma_conta`;
- `AccountNumber_*` e `TransactionOwner_*` em `ValueObjectTests`;
- `DefaultCategories_*` e a contagem de categorias padrão;
- `PersistenceTests` de índices/checks sobre `AccountId`;
- `LogPrivacyTests` (passa a usar OFX de cartão).

## 9. Proposta

O plano aprovado está resumido nas fases a seguir; decisões com impacto duradouro são registradas em ADRs (0011–0015).

| Fase | Entrega |
|---|---|
| 2 | Novo domínio (`Invoice`, `InstallmentPurchase`, `CategoryRule`, `SpendingLimit`, `FinancialGoal`, `RecurringExpense`, `TransactionKind`), remoção de contas, nova migration `InitialCreate` e reset automático de bancos antigos (com backup) |
| 3 | Importação somente de cartão, com identificação de fatura, tipo, estabelecimento e parcelas |
| 4 | Regras de categorização configuráveis e aprendizado a partir da classificação manual |
| 5 | Dashboard redesenhado, análises (média, variação, distribuição, aumentos), faturas, parcelamentos, relatórios |
| 6 | Limites, recorrentes, oportunidades de economia, meta e simulador de cenários |
| 7 | Cobertura das regras críticas |
| 8 | Remoção de código sem uso, documentação e backup local |

Decisões tomadas com o usuário:

- **Banco**: migrations resetadas (nova `InitialCreate`); um banco antigo é movido para `backups/` e recriado.
- **Mês das análises**: competência da fatura (mês de vencimento).
- **PDF**: adiado (OFX e CSV continuam suportados).
