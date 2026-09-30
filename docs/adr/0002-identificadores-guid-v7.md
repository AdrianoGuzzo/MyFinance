# ADR 0002 — Identificadores Guid v7

- Status: aceita
- Data: 2026-09-30

## Contexto

Chaves inteiras autoincrementais geram conflito quando dados de múltiplos dispositivos forem sincronizados (fase 5). GUIDs aleatórios (v4) fragmentam índices.

## Decisão

Todas as entidades usam `Guid.CreateVersion7()`, gerado no domínio (não pelo banco).

## Consequências

- Entidades têm identidade antes de serem persistidas (útil em agregados como `Import`).
- IDs ordenáveis por tempo, sem conflito entre dispositivos.
- IDs maiores que inteiros (irrelevante no volume de dados pessoal).
