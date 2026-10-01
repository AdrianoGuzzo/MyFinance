# MyFinance

Ferramenta desktop pessoal de **análise e estratégia de gastos de cartão de crédito**. **Todo o processamento e os dados ficam no seu computador** — sem nuvem, sem telemetria, sem envio de dados financeiros para serviços externos.

> A pergunta que o app responde: *"se eu continuar gastando dessa forma, para onde meu dinheiro está indo e onde existem oportunidades concretas de reduzir despesas?"*
>
> Fora do escopo: saldo bancário, conta corrente, PIX, investimentos, contas a pagar/receber e Open Finance.

## O que faz

| Área | |
|---|---|
| Importação | Faturas e extratos de cartão em **OFX** ou **CSV** (PDF ainda não — [ADR 0014](docs/adr/0014-importacao-pdf-adiada.md)), com prévia, detecção de duplicidades, identificação da fatura, do tipo (compra, estorno, pagamento, tarifa, juros) e das parcelas |
| Categorização | Categorias e subcategorias de gastos, regras automáticas configuráveis ("IFOOD" → Alimentação › Delivery) e sugestão de regra após uma classificação manual |
| Faturas | Uma fatura por cartão e mês, com total, situação (aberta, fechada, paga, vencida) e lançamentos |
| Parcelamentos | Compras parceladas reconstruídas a partir das parcelas importadas; valor restante e comprometimento das próximas faturas |
| Dashboard | Gastos do mês, média, variação, maiores gastos, o que aumentou/diminuiu, evolução (3/6/12 meses), próximas faturas, oportunidades e insights |
| Estratégia | Limites por categoria, gastos recorrentes (Essencial/Opcional/Avaliar), objetivo de economia, possíveis oportunidades e simulador de cenários |
| Relatórios | Por categoria, subcategoria, estabelecimento, evolução mensal, parcelamentos e recorrentes |

Todas as análises usam a **competência da fatura** (mês de vencimento) e o **valor de gasto**: compras, tarifas e juros menos estornos; pagamentos de fatura não contam ([ADR 0012](docs/adr/0012-regras-de-analise-de-gastos.md)). As oportunidades são **informação** para você decidir — o app nunca trata uma redução como obrigatória.

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

```text
Desktop ──► Application ──► Domain
   │             ▲
   └──► Infrastructure
```

- **Domain** — entidades (cartão, fatura, transação, compra parcelada, categoria, regra, limite, meta, recorrente) e regras puras: duplicidade, parcelas, estabelecimento, fatura, análises (`Domain/Analysis`). Sem dependências externas.
- **Application** — casos de uso e portas (`ISpendingQueries`, `ITransactionImporter`, `IDatabaseBackup`...).
- **Infrastructure** — EF Core/SQLite, importadores OFX/CSV, logging, backup.
- **Desktop** — Views/ViewModels (MVVM) e composição da aplicação.

Detalhes em [docs/architecture.md](docs/architecture.md). A análise que motivou o refoco está em [docs/analise-refoco-cartao.md](docs/analise-refoco-cartao.md).

## Como executar

```bash
dotnet run --project src/MyFinance.Desktop                                   # perfil Development (launchSettings)
dotnet run --project src/MyFinance.Desktop --launch-profile "MyFinance (Production)"
```

Na primeira execução o banco é criado (migrations) e as categorias e regras padrão são cadastradas. Um banco da versão anterior (com contas bancárias) é **movido** para `backups/` e recriado — nada é apagado.

| | Production (padrão) | Development |
|---|---|---|
| Banco | `%LOCALAPPDATA%\MyFinance\myfinance.db` | `%LOCALAPPDATA%\MyFinance\data\myfinance-dev.db` |
| Logs | `%LOCALAPPDATA%\MyFinance\logs\myfinance-AAAAMMDD.log` | idem, nível Debug |
| Configuração | `appsettings.json` | + `appsettings.Development.json` |

O ambiente vem de `DOTNET_ENVIRONMENT`. Caminhos relativos em `Database:Path`/`Logs:Directory` são resolvidos a partir de `%LOCALAPPDATA%\MyFinance` (no Linux/macOS, a pasta equivalente de dados locais). Também podem ser passados na linha de comando (`--Database:Path=...`).

Menu: Dashboard · Gastos · Faturas · Cartões · Parcelamentos | Categorias · Limites · Recorrentes · Estratégia | Relatórios | Importação | Configurações (tema e backup).

## Como executar os testes

```bash
dotnet test                                      # todos
dotnet test --project tests/MyFinance.Domain.Tests
```

## Migrations e banco de dados

```bash
dotnet tool restore
dotnet ef migrations add <Nome> --project src/MyFinance.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/MyFinance.Infrastructure
```

A aplicação aplica migrations pendentes na inicialização; antes disso, grava uma cópia do banco existente em `backups/`. Detalhes em [docs/database.md](docs/database.md).

## Como importar uma fatura

1. Cadastre o cartão (4 últimos dígitos, dia de fechamento e de vencimento).
2. No app/site do banco, exporte a fatura ou o extrato do cartão em **OFX** (recomendado) ou **CSV**.
3. *Importação* → selecione o arquivo → confira o cartão (sugerido pelos 4 últimos dígitos do OFX) e a fatura (pela data de cada lançamento, ou uma fatura escolhida para o arquivo inteiro).
4. Revise a prévia (estabelecimento, parcela, fatura, tipo, novas/duplicadas/com erro) → *Confirmar*.

Reimportar o mesmo arquivo ou um período sobreposto não cria duplicados. Faturas CSV com compras positivas recebem a sugestão de inverter os sinais. Formatos e regras: [docs/import.md](docs/import.md).

## Privacidade

- Dados apenas no SQLite local; nenhum cliente HTTP, telemetria ou analytics.
- Logs só com metadados técnicos — verificado pelo teste `LogPrivacyTests`.
- Do cartão só se guardam os 4 últimos dígitos.
- *Configurações › Fazer backup* grava uma cópia local do banco no destino que você escolher.
- O banco não é criptografado nesta versão ([ADR 0015](docs/adr/0015-privacidade-backup-e-criptografia.md)).

## Decisões arquiteturais

| ADR | Decisão |
|---|---|
| [0001](docs/adr/0001-arquitetura-em-camadas.md) | Arquitetura em camadas com domínio isolado |
| [0002](docs/adr/0002-identificadores-guid-v7.md) | Identificadores `Guid` v7 |
| [0003](docs/adr/0003-datas-e-valores-monetarios.md) | `DateOnly` para datas financeiras, `decimal` com sinal para valores |
| [0004](docs/adr/0004-deteccao-de-duplicidades.md) | Detecção de duplicidades em três estratégias com consumo |
| [0005](docs/adr/0005-fatura-de-cartao-calculada.md) | ~~Fatura calculada~~ — substituída pela 0011 |
| [0006](docs/adr/0006-microsoft-testing-platform.md) | Microsoft.Testing.Platform para executar testes |
| [0007](docs/adr/0007-decimal-no-sqlite.md) | `decimal` armazenado como TEXT no SQLite |
| [0008](docs/adr/0008-parsers-proprios-ofx-csv.md) | Parsers próprios de OFX e CSV, sem regras por banco |
| [0009](docs/adr/0009-transferencias-e-regras-do-dashboard.md) | ~~Transferências e regras do dashboard~~ — substituída pela 0012 |
| [0010](docs/adr/0010-interface-desktop.md) | Composição e padrões da interface desktop |
| [0011](docs/adr/0011-faturas-persistidas.md) | Faturas persistidas com situação e total derivados |
| [0012](docs/adr/0012-regras-de-analise-de-gastos.md) | Competência da fatura, valor de gasto e tipos de lançamento |
| [0013](docs/adr/0013-parcelamentos-e-projecao.md) | Parcelamentos reconstruídos a partir das parcelas e projeção |
| [0014](docs/adr/0014-importacao-pdf-adiada.md) | Importação de PDF adiada |
| [0015](docs/adr/0015-privacidade-backup-e-criptografia.md) | Privacidade, backup local e criptografia adiada |

## Limitações conhecidas

- **PDF** não é importado ([ADR 0014](docs/adr/0014-importacao-pdf-adiada.md)).
- **Mesmo lançamento em OFX e CSV** com descrições diferentes (ex.: OFX com `NAME - MEMO`) não é reconhecido como duplicado pela estratégia de dados; reimportações no mesmo formato são.
- **Parcela no formato `03/12` sem a palavra "parcela"** pode ser confundida com uma data `dd/MM` no fim da descrição.
- **Pagamento da fatura** é registrado manualmente (*Faturas › Marcar como paga*); pagamentos importados aparecem como informação na fatura em que foram lançados.
- **Testes de interface**: ViewModels ainda sem projeto de testes automatizados.
