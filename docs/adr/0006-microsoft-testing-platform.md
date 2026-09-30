# ADR 0006 — Microsoft.Testing.Platform para executar testes

- Status: aceita
- Data: 2026-09-30

## Contexto

O xUnit v3 (4.x) roda sobre o Microsoft.Testing.Platform (MTP). No SDK do .NET 10, `dotnet test` no modo VSTest não é mais suportado para projetos MTP.

## Decisão

- `global.json` configura `"test": { "runner": "Microsoft.Testing.Platform" }`.
- Projetos de teste são executáveis (`OutputType=Exe`) e não referenciam `Microsoft.NET.Test.Sdk` nem `xunit.runner.visualstudio`.
- Configuração comum em `tests/Directory.Build.props`.

## Consequências

- `dotnet test` e `dotnet test --project <projeto>` funcionam direto.
- IDEs recentes (VS 2026, Rider, VS Code C# Dev Kit) suportam MTP nativamente.
