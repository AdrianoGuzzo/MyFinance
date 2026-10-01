namespace MyFinance.Domain.Enums;

public enum CardBrand
{
    Visa = 1,
    Mastercard = 2,
    Elo = 3,
    Amex = 4,
    Hipercard = 5,
    Other = 99,
}

/// <summary>
/// Natureza do lançamento no cartão. O sinal do valor continua valendo
/// (negativo = débito no cartão, positivo = crédito), e cada tipo exige o sinal correspondente.
/// </summary>
public enum TransactionKind
{
    /// <summary>Compra (valor negativo).</summary>
    Purchase = 1,

    /// <summary>Estorno ou crédito de uma compra (valor positivo). Reduz os gastos.</summary>
    Refund = 2,

    /// <summary>Pagamento da fatura (valor positivo). Não é gasto nem reduz gastos.</summary>
    Payment = 3,

    /// <summary>Tarifa, anuidade ou IOF (valor negativo).</summary>
    Fee = 4,

    /// <summary>Juros, encargos ou multa (valor negativo).</summary>
    Interest = 5,

    /// <summary>Ajuste manual (qualquer sinal).</summary>
    Adjustment = 6,
}

/// <summary>Situação de uma fatura, derivada das datas e do pagamento.</summary>
public enum InvoiceStatus
{
    /// <summary>Ainda recebe lançamentos (antes do fechamento).</summary>
    Open = 1,

    /// <summary>Fechada, aguardando pagamento até o vencimento.</summary>
    Closed = 2,

    Paid = 3,

    /// <summary>Vencida sem pagamento registrado.</summary>
    Overdue = 4,
}

/// <summary>Consumo de um limite de gastos.</summary>
public enum LimitStatus
{
    /// <summary>Abaixo de 80% do limite.</summary>
    Within = 1,

    /// <summary>Entre 80% e 100% do limite.</summary>
    Near = 2,

    /// <summary>Acima do limite.</summary>
    Exceeded = 3,
}

/// <summary>Classificação de um gasto recorrente feita pelo usuário.</summary>
public enum RecurringClassification
{
    Unclassified = 0,
    Essential = 1,
    Optional = 2,
    Evaluate = 3,
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

    /// <summary>Mesma combinação de cartão, data, valor e descrição.</summary>
    SameData = 3,

    /// <summary>Repetida dentro do próprio arquivo (mesmo ExternalId e mesmos dados).</summary>
    RepeatedInFile = 4,
}