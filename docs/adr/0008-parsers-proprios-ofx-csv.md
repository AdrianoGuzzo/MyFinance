# ADR 0008 — Parsers próprios de OFX e CSV

- Status: aceita
- Data: 2026-09-30

## Contexto

Os requisitos pedem para não introduzir bibliotecas sem justificativa. Bibliotecas OFX para .NET são pouco mantidas e costumam falhar com o SGML "solto" de bancos brasileiros (tags de valor sem fechamento, `CHARSET:1252`, vírgula decimal). Bibliotecas CSV (ex.: CsvHelper) resolvem bem o parsing, mas o trabalho difícil aqui é **interpretar** o conteúdo (cabeçalhos em português, números BR/US, datas), que seria escrito de qualquer forma.

## Decisão

- **OFX**: tokenizer próprio e tolerante (~100 linhas) que trata SGML e XML com a mesma regra: texto após uma tag de abertura cria uma folha fechada implicitamente; fechamentos sem abertura correspondente são ignorados. Buscas de valores são feitas em descendentes para tolerar folhas vazias sem fechamento.
- **CSV**: leitor RFC 4180 próprio + mapeamento de colunas por nomes de cabeçalho conhecidos.
- **Sem regras por banco**: nenhum "importador Nubank". Os nomes de colunas cobrem exportações comuns; o Nubank é só um dos formatos testados.
- Importadores mantêm o sinal do arquivo; a decisão de inverter (faturas CSV) é da aplicação, que conhece o destino.

## Consequências

- Zero dependências novas; comportamento totalmente coberto por testes (arquivos reais anonimizados/fictícios e casos de borda).
- Novos cabeçalhos de CSV exigem adicionar nomes em `CsvColumnMap`. Uma tela de mapeamento manual de colunas pode ser adicionada depois sem mudar o contrato `ITransactionImporter`.
- Não há validação contra o schema OFX; o parser aceita arquivos "quase OFX", o que é desejável para extratos bancários reais.
