# ADR 0016 — Servidor MCP local para assistentes de IA

- Status: aceita
- Data: 2026-10-01
- Substitui parcialmente: [ADR 0015](0015-privacidade-backup-e-criptografia.md) (itens "nenhum código de rede" e "IA futura")

## Contexto

O usuário quer que um assistente de IA (Claude Code, Claude Desktop ou outro cliente MCP) **analise os gastos** e **organize a categorização** do MyFinance. A ADR 0015 previa que qualquer uso de IA veria só dados agregados, sem descrições nem estabelecimentos. Com isso, categorizar fica impossível: a IA precisa ler a descrição para escolher a categoria.

## Decisão

- **Servidor MCP dentro do aplicativo desktop** (projeto `MyFinance.Mcp`), com o SDK oficial em C# (`ModelContextProtocol.AspNetCore`) e transporte Streamable HTTP **sem sessão** em `/mcp`.
- **Desligado por padrão. Ligar é o consentimento.** O usuário liga em *Configurações › Servidor MCP*. A escolha fica em `usersettings.json`, e o servidor volta a iniciar sozinho quando o app abre, depois das migrations. `Mcp:Enabled` e `Mcp:Port` no appsettings ou na linha de comando têm precedência e travam a opção na tela.
- **Acesso completo.** O assistente vê descrições, estabelecimentos e valores. O que ele lê **sai do computador** pelo cliente de IA, rumo ao provedor do modelo. A tela de Configurações avisa isso.
- **Somente local.** Kestrel escuta apenas em `127.0.0.1`. A porta padrão é **47821** (abaixo da faixa efêmera do Windows e fora das portas dos templates `dotnet new`) e pode ser trocada entre 1024 e 65535. Porta ocupada ou reservada gera mensagem amigável.
- **Proteções do `LocalRequestGuard`:**
  - O `Host` precisa ser `127.0.0.1` ou `localhost`, como proteção contra DNS rebinding.
  - O `Origin`, quando presente, também precisa ser local. Assim, páginas da web não conseguem chamar o servidor.
  - O token `Authorization: Bearer` é **obrigatório**: 32 bytes aleatórios, gerados quando o servidor é ligado pela primeira vez, comparados em tempo constante e trocáveis pelo botão *Gerar novo token*. A porta local é alcançável por qualquer processo ou usuário da máquina, enquanto o banco é protegido pelo perfil do usuário. O token devolve essa mesma proteção. Ele já vem no comando e na configuração copiados da tela.
- **Catálogo fechado.** As classes de ferramentas são registradas explicitamente (`McpComposition.ToolTypes`), nunca por varredura do assembly. Um teste garante o conjunto.
  - **Leitura:** resumo do mês, relatórios por categoria, estabelecimento e evolução, cartões, faturas, parcelamentos, busca de lançamentos, pendentes de categoria, categorias, regras, estratégia, simulação, limites e recorrentes.
  - **Escrita permitida:** categorizar (um lançamento ou em lote, tudo ou nada), criar categoria, criar, editar, ativar e desativar regras, aplicar regras aos pendentes, meta, limites e classificação de recorrentes.
  - **Não expostos:** importação, edição de cartões, marcar fatura como paga, troca do tipo de lançamento, exclusões de lançamento, categoria ou regra, backup.
- **Composição.** O servidor tem seu próprio contêiner de serviços (`AddInfrastructure` + `AddApplication`), com um DbContext por requisição sobre o mesmo arquivo SQLite. Compartilha com o app o `ILoggerFactory`, o `TimeProvider` e o `DataChangeNotifier`. Depois de cada escrita, o app recarrega a tela aberta, uma vez por lote.
- **Erros.** `ValidationException` e `DomainException` viram `McpException`, e a IA recebe a mensagem amigável para se corrigir. Os demais erros são registrados com o nome da ferramenta e chegam como erro genérico.
- **Logs.** Nunca contêm argumentos, resultados ou o token. O host aplica filtros próprios (`ModelContextProtocol`, `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `Microsoft.Hosting` em Warning), porque o SDK registra o JSON das mensagens em Debug. Isso é verificado por teste com captura em nível Trace.
- **Concorrência no SQLite.** O journal continua o padrão, sem WAL. O Microsoft.Data.Sqlite repete operações bloqueadas até o tempo limite do comando (30 s), e as escritas são pequenas e de um único usuário. WAL fica como evolução, se surgirem bloqueios.

## Consequências

- O app passa a depender do runtime compartilhado do ASP.NET Core, que acompanha o SDK do .NET.
- O usuário decide, a cada assistente, se aceita que os dados de gastos sejam enviados ao provedor da IA. Desligar o servidor encerra o acesso na hora. Gerar um novo token revoga os clientes já configurados.
- O Claude Desktop conecta servidores locais por processo e usa o `mcp-remote` (Node.js) como ponte HTTP. O Claude Code conecta direto por HTTP.