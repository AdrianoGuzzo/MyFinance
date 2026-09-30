namespace MyFinance.Domain.Enums;

public enum AccountType
{
    Checking = 1,
    Savings = 2,
    Payment = 3,
    Investment = 4,
}

public enum CategoryType
{
    Expense = 1,
    Income = 2,

    /// <summary>
    /// Movimentação entre contas/cartões do próprio usuário (ex.: pagamento de fatura).
    /// Não conta como receita nem despesa, evitando contar a mesma compra duas vezes.
    /// </summary>
    Transfer = 3,
}

/// <summary>
/// Derivado do sinal do valor: positivo = entrada (Income), negativo = saída (Expense).
/// </summary>
public enum TransactionType
{
    Income = 1,
    Expense = 2,
}

public enum TransactionOwnerType
{
    Account = 1,
    CreditCard = 2,
}

public enum ImportFileType
{
    Ofx = 1,
    Csv = 2,
}

public enum ImportStatus
{
    Pending = 1,
    Completed = 2,
    Cancelled = 3,
    Failed = 4,
}

public enum ImportTransactionStatus
{
    /// <summary>Ainda não existe; será criada ao confirmar a importação.</summary>
    New = 1,

    /// <summary>Já existe um lançamento equivalente; não será criada.</summary>
    Duplicate = 2,

    /// <summary>Linha do arquivo não pôde ser convertida em lançamento válido.</summary>
    Invalid = 3,

    /// <summary>Lançamento criado com sucesso.</summary>
    Imported = 4,
}

/// <summary>
/// Estratégia que identificou a duplicidade, em ordem de prioridade.
/// </summary>
public enum DuplicateReason
{
    None = 0,

    /// <summary>Mesmo identificador fornecido pelo banco (ex.: FITID do OFX).</summary>
    ExternalId = 1,

    /// <summary>Mesmo hash dos dados relevantes da transação.</summary>
    ImportHash = 2,

    /// <summary>Mesma combinação de conta/cartão, data, valor e descrição.</summary>
    SameData = 3,

    /// <summary>Repetida dentro do próprio arquivo (mesmo ExternalId e mesmos dados).</summary>
    RepeatedInFile = 4,
}