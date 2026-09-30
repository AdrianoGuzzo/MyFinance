# Importação de extratos

## Fluxo

```text
Selecionar arquivo → Identificar formato → Ler arquivo (+ SHA-256)      ImportService.AnalyzeAsync
→ Selecionar conta/cartão (pré-selecionado se o arquivo identificar)
→ Validar transações → Detectar duplicidades → Mostrar prévia           ImportService.PreviewAsync
→ Usuário confirma → Persistir → Exibir resultado                        ImportService.ConfirmAsync
```

- O arquivo é lido **antes** da escolha da conta para permitir sugerir o destino: `ACCTID` do OFX comparado (só dígitos) com o número das contas; para cartões, os 4 últimos dígitos.
- A prévia pode ser recalculada quantas vezes for preciso (trocar conta, inverter sinais) sem gravar nada.
- **Cancelar** = não chamar `ConfirmAsync`. Nada é gravado até a confirmação.
- Na confirmação, validação e duplicidades são **recalculadas** (o banco pode ter mudado desde a prévia — ex.: duas prévias do mesmo arquivo abertas). Importação, itens e lançamentos são gravados em um único `SaveChanges` (transação única).
- Uma prévia só pode ser confirmada uma vez. Arquivo sem nenhuma transação não pode ser confirmado.
- Lançamentos novos passam pelo `ICategorizationService` (no MVP, não sugere nada).

### Validação

Além dos erros de leitura do importador, cada transação passa pelas regras do domínio (as mesmas usadas ao criar o lançamento): valor zero, mais de 2 casas decimais, descrição/identificador longos demais. Itens inválidos aparecem na prévia com a mensagem e são gravados como histórico (`ImportTransaction` com status `Invalid`).

### Logs

`ImportStarted` (extensão e 8 primeiros caracteres do hash), `ImportCompleted` (contagens e duração), `ImportRejected` (arquivo inválido, com a mensagem amigável) e `ImportFailed` (erro técnico, com exceção). Nunca são registrados nome do arquivo, descrições, valores ou números de conta.

## Modelo neutro

Cada importador (`OfxTransactionImporter`, `CsvTransactionImporter`) converte o arquivo para `ImportedTransaction`, independente do formato ou banco de origem. A aplicação valida esses itens e cria o agregado `Import` com seus `ImportTransaction`.

## Hash do arquivo

`SHA-256` do conteúdo do arquivo é gravado em `Import.FileHash`. Ao importar um arquivo com hash já conhecido, a aplicação avisa:

```text
Este arquivo já foi importado.
Data da importação: 30/09/2026
Transações: 127
```

O usuário pode continuar e revisar a prévia — a detecção por transação garante que nada será duplicado.

## Detecção de duplicidades

Implementada em `MyFinance.Domain.Services.DuplicateDetector` (algoritmo puro, sem banco). Estratégias, em ordem de prioridade:

| # | Estratégia | Chave | Quando ajuda |
|---|---|---|---|
| 1 | `ExternalId` | identificador do banco (FITID no OFX) | Reimportação de OFX, mesmo com descrição alterada pelo banco |
| 2 | `ImportHash` | SHA-256 de data + valor + descrição normalizada + saldo (se houver) | Reimportação de CSV sem identificador |
| 3 | `SameData` | conta/cartão + data + valor + descrição normalizada | Mesmo lançamento vindo de formato diferente (OFX x CSV) ou criado manualmente |

Regras importantes:

- **Consumo**: cada transação existente só casa com uma candidata. Duas compras idênticas no arquivo e uma no banco → uma duplicada e uma nova.
- **ExternalIds diferentes nunca casam** pelas estratégias 2 e 3: se o banco diz que são transações distintas, elas são.
- **Identificador reutilizado pelo banco**: com `ExternalId` repetido (no arquivo ou no banco), a duplicidade exige também data, valor e descrição iguais; com identificador único, basta o valor coincidir.
- **Repetição no próprio arquivo**: o mesmo `ExternalId` **com os mesmos dados** duas vezes no arquivo marca a segunda como `RepeatedInFile`. Mesmo `ExternalId` com dados diferentes não é repetição (alguns bancos reutilizam FITIDs).
- A descrição é normalizada (maiúsculas, sem acentos, espaços colapsados) antes de comparar.
- A comparação é sempre restrita à mesma conta/cartão.

Resultado exibido ao usuário:

```text
127 transações encontradas
121 novas
6 duplicadas
0 com erro
```

## Formatos

A escolha do importador é feita pela extensão do arquivo (`TransactionImporterResolver`). Extensão desconhecida → *"O formato do arquivo não é reconhecido."*

### Leitura comum

- Encoding: BOM UTF-8/UTF-16 é respeitado; sem BOM, tenta UTF-8 e, se houver bytes inválidos, usa **Windows-1252** (padrão de muitos bancos brasileiros).
- Limite de 20 MB por arquivo.
- Arquivo vazio (ou só espaços) → *"O arquivo está vazio."*
- Valores: formato brasileiro e americano (`-120,50`, `1.234,56`, `1,234.56`, `R$ -35,90`, `(35,90)`). O separador que aparece por último é o decimal.
- **Valores ambíguos são recusados**: `1.500` ou `1,500` (separador único seguido de exatamente 3 dígitos) podem ser mil e quinhentos ou um e meio. Em vez de adivinhar — e gravar um valor mil vezes menor — o registro vira erro na prévia. Valores como `1.500,00`, `1500.00` ou `1,5` não são ambíguos.

### OFX (`.ofx`, `.qfx`)

| Suporte | |
|---|---|
| OFX 1.x SGML (tags de valor sem fechamento) | ✔ |
| OFX 1.x com tags fechadas (Nubank e outros) | ✔ |
| OFX 2.x XML | ✔ |
| Extrato de conta (`STMTRS`) e de cartão (`CCSTMTRS`) | ✔ — informado em `ImportResult.StatementKind` |

Mapeamento de cada `<STMTTRN>`:

| OFX | `ImportedTransaction` |
|---|---|
| `DTPOSTED` | `Date` — somente os 8 primeiros dígitos (`yyyyMMdd`), sem converter fuso |
| `TRNAMT` | `Amount` (sinal do arquivo; aceita vírgula decimal) |
| `NAME` / `MEMO` | `Description` — se um contém o outro, usa o mais longo; senão `NAME - MEMO` |
| `FITID` | `ExternalId` |
| todas as folhas | `RawData` (`TAG=valor;...`) |

`ACCTID` da conta/cartão vai para `ImportResult.StatementAccountId`, para sugerir a conta de destino (dado sensível, nunca registrado em log).

Erros de arquivo (exceção): marcação `<OFX>` ausente; nenhum extrato de conta ou cartão; **mais de um extrato no mesmo arquivo** (ex.: conta e cartão juntos) — importar tudo para um único destino misturaria contas.

### CSV (`.csv`)

- Delimitador detectado na primeira linha: `;`, `,` ou tab.
- RFC 4180: campos entre aspas podem conter delimitador, quebra de linha e aspas duplicadas (`""`).
- A primeira linha **deve** ser um cabeçalho. Colunas reconhecidas (sem diferenciar maiúsculas/acentos):

| Campo | Nomes aceitos | Obrigatório |
|---|---|---|
| Data | Data, Date, DT, Data Lançamento, Data de Lançamento, Data da Transação, Data Movimento, Transaction Date | ✔ |
| Valor | Valor, Amount, Value, Valor (R$), Valor R$, Quantia | ✔ |
| Descrição | Descrição, Description, Title, Histórico, Lançamento, Estabelecimento, Memo, Detalhes | ✔ |
| Identificador | Identificador, ID, FITID, Código, Transaction ID, ID da Transação | |
| Saldo | Saldo, Balance, Saldo (R$) | |

- Datas: `dd/MM/yyyy`, `d/M/yyyy`, `dd/MM/yy`, `yyyy-MM-dd`, `dd-MM-yyyy`, `dd.MM.yyyy`, `yyyy/MM/dd`; horário, se houver, é descartado. O formato americano `MM/dd/yyyy` **não** é aceito (ambíguo).
- Colunas do cabeçalho que o importador não usa são ignoradas; linhas em branco são puladas.
- **Linha com mais colunas que o cabeçalho** (ex.: `01/01/2026,-10,50,Padaria` num CSV separado por vírgula) é recusada, porque os valores estariam deslocados. Colunas extras vazias no fim da linha são toleradas.
- Colunas separadas de crédito/débito ainda não são suportadas.

Exemplos testados (dados fictícios em `tests/MyFinance.Infrastructure.Tests/TestFiles`):

```csv
Data,Valor,Identificador,Descrição
29/09/2026,-120.50,6512a1f0-...,Compra no débito - Supermercado
```

```csv
date,title,amount
2026-09-27,Netflix.com,49.90
```

```csv
Data Lançamento;Histórico;Valor (R$);Saldo (R$)
02/09/2026;"PIX ENVIADO; ALUGUEL";-1.500,00;500,00
```

### Sinal dos valores em faturas CSV

Faturas de cartão em CSV (ex.: `date,title,amount`) costumam trazer **compras positivas** e pagamentos negativos — o oposto da convenção do sistema e do OFX. O importador **não** altera o sinal (ele não sabe o destino). A aplicação sugere inverter os sinais quando o destino é um cartão e a maioria dos valores do arquivo é positiva; o usuário confirma na prévia.

### Erros por registro

Registros que não podem ser convertidos não interrompem a importação: vão para `ImportResult.Errors` com a posição (linha no CSV, ordem da transação no OFX), a mensagem e o conteúdo original.

| Situação | Mensagem |
|---|---|
| Data ausente/inválida | `Data inválida: "31/02/2026".` |
| Valor inválido | `Valor inválido: "dez reais".` |
| Sem descrição (OFX: NAME e MEMO vazios) | `Transação sem descrição.` |
| Colunas faltando (CSV) | `Linha com 2 coluna(s); esperado ao menos 3.` |
| Colunas a mais (CSV) | `Linha com 4 coluna(s); o cabeçalho tem 3.` |

Mensagens citam no máximo 40 caracteres do valor original (campos quebrados podem ter milhares).

Regras de negócio (valor zero, mais de 2 casas decimais) são validadas depois, pela aplicação, com as regras do domínio.

## Adicionando um novo formato

1. Implementar `ITransactionImporter` em `Infrastructure/Imports/<Formato>` produzindo `ImportedTransaction`.
2. Registrar em `DependencyInjection.AddImporters`.
3. Nenhuma mudança em domínio, duplicidade, prévia ou persistência. O mesmo vale para uma futura fonte Open Finance.
