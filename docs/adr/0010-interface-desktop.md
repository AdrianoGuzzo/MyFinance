# ADR 0010 — Composição e padrões da interface desktop

- Status: aceita
- Data: 2026-09-30

## Contexto

A interface (Avalonia 12 + CommunityToolkit.Mvvm) precisa: ViewModels sem regra de negócio, DbContext com vida curta, erros nunca derrubando a aplicação, confirmação antes de operações destrutivas, tema claro/escuro, configuração por ambiente e logs locais — sem bibliotecas extras além da stack definida.

## Decisões

1. **Host genérico** (`Microsoft.Extensions.Hosting`) apenas para composição: DI, `appsettings.json` + `appsettings.{Ambiente}.json` (`DOTNET_ENVIRONMENT`) e Serilog. Nenhum serviço hospedado roda em segundo plano.
2. **Um escopo de DI por operação** (`IUseCaseExecutor`): ViewModels vivem a execução toda (singletons); cada chamada de caso de uso abre um escopo com um `DbContext` novo. Evita entidades rastreadas acumuladas e dados desatualizados entre telas.
3. **Tratamento de erros centralizado** em `PageViewModel.RunAsync`: toda operação de tela passa por ele. `ErrorMessages` converte a exceção em mensagem amigável por tipo (validação, importação, persistência, técnico); apenas erros técnicos são registrados como `TechnicalError`. Handlers globais (`AppDomain`, `TaskScheduler`, `Dispatcher.UIThread`) registram `UnhandledException` como último recurso e o thread de UI marca a exceção como tratada.
4. **Diálogos como sobreposição** na janela principal (`DialogService` + `DialogViewModel`), aguardáveis (`Task<bool>`), sem janelas extras nem pacotes de message box. Usados para confirmação de desativações, erros e resultado da importação.
5. **Views resolvidas por `DataTemplate` tipado** no `App.axaml` (sem ViewLocator por reflexão) e **bindings compilados** (padrão no Avalonia 12): erros de binding aparecem na compilação.
6. **Gráficos nativos**: `BarChart` e `LineChart` desenham via `DrawingContext`; despesas por categoria usam `ProgressBar`. Sem LiveCharts/OxyPlot (compatibilidade com Avalonia 12 não confirmada e dependência desnecessária para três gráficos simples).
7. **Tema** via `RequestedThemeVariant` (sistema/claro/escuro), cores próprias em `ThemeDictionaries`; preferência gravada em `usersettings.json` na pasta de dados do usuário.
8. **Cultura** pt-BR configurável (`App:Culture`), aplicada no início: moeda e datas nas bindings usam `StringFormat`.
9. **Arquivos do usuário** (banco, logs, preferências) ficam em `%LOCALAPPDATA%\MyFinance`; caminhos relativos no appsettings são resolvidos a partir dessa pasta — nunca da pasta do executável, que pode ser somente leitura.

## Consequências

- ViewModels testáveis sem Avalonia real (dependem de `IUseCaseExecutor`, `IDialogService`, `IFilePickerService`); ainda sem projeto de testes de UI no MVP.
- Code-behind limitado a `InitializeComponent` e à composição em `App.axaml.cs`.
- As regras CA1822 e CA1859 estão desativadas apenas no projeto Desktop (bindings exigem propriedades de instância; comandos gerados exigem `Task`).

## Armadilhas de binding (encontradas em uso real)

Exceções lançadas dentro de bindings são capturadas pelo Avalonia: a tela continua funcionando, nada chega ao log, mas o depurador interrompe. Duas regras evitam essa classe de erro:

1. **`<Run Text="{Binding ..., Mode=OneWay}"/>`** — `Run.Text` grava de volta na origem por padrão; com `StringFormat` ou conversores, o Avalonia tenta converter o texto exibido ("R$ 174,80", "Conta de pagamento", número mascarado) de volta para `decimal`/enum/value object.
2. **Propriedades ligadas a `ComboBox.SelectedItem` são nuláveis** e lidas com `?.` — ao recarregar a lista de itens, o ComboBox grava `null` na propriedade.

Verificação usada: execução *headless* (Avalonia.Headless) de todas as telas com captura de exceções de primeira chance e do log de bindings do Avalonia.
