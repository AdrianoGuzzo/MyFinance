# ADR 0001 — Arquitetura em camadas com domínio isolado

- Status: aceita
- Data: 2026-09-30

## Contexto

A aplicação começa como desktop local (SQLite, OFX/CSV), mas deve evoluir para regras automáticas, IA, Open Finance, sincronização e mobile/web sem reescrever o domínio.

## Decisão

Quatro projetos: `Domain`, `Application`, `Infrastructure`, `Desktop`. O domínio não referencia nenhum pacote. A Application define contratos (portas) implementados pela Infrastructure. O Desktop é o único composition root.

Não adotamos Mediator/CQRS nem bibliotecas de mapeamento: casos de uso são classes simples injetadas nos ViewModels.

## Consequências

- Domínio testável sem banco nem UI.
- Trocar SQLite, adicionar uma API ou um app mobile reutiliza Domain e Application.
- Um pouco mais de cerimônia (interfaces de repositório) do que um projeto único.
