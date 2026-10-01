# ADR 0014 — Importação de PDF adiada

- Status: aceita
- Data: 2026-10-01

## Contexto

O pedido prioriza CSV, OFX e PDF. Faturas em PDF variam por banco (layout, colunas, datas sem ano, valores em várias colunas), muitas vezes são protegidas por senha e exigiriam uma biblioteca nova (ex.: PdfPig), contrariando a [ADR 0008](0008-parsers-proprios-ofx-csv.md) sem uma heurística confiável.

## Decisão

- Esta versão importa **OFX e CSV**. PDF fica fora.
- O ponto de extensão permanece: um `PdfTransactionImporter` implementaria `ITransactionImporter` e produziria `ImportedTransaction`, reaproveitando prévia, fatura, parcelas, categorização e duplicidade. A escolha da fatura para o arquivo inteiro (na prévia) já cobre PDFs sem ano nas datas.

## Consequências

- Quem só tem PDF precisa exportar OFX/CSV no app do banco (a maioria oferece).
- Ao implementar PDF: começar por um banco com amostra anonimizada, tratar senha e manter erros por linha na prévia.
