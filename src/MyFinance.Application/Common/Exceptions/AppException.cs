namespace MyFinance.Application.Common.Exceptions;

/// <summary>
/// Erro esperado da aplicação, com mensagem amigável ao usuário.
/// Qualquer exceção que não derive desta classe (nem de <c>DomainException</c>) é tratada como erro técnico.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }

    protected AppException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>Dados informados pelo usuário são inválidos (ex.: nome de categoria já existe).</summary>
public sealed class ValidationException : AppException
{
    public ValidationException(string message) : base(message) { }

    public ValidationException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>O arquivo não pôde ser importado (formato não reconhecido, arquivo vazio...).</summary>
public sealed class ImportException : AppException
{
    public ImportException(string message) : base(message) { }

    public ImportException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>Falha ao ler ou gravar no banco de dados local.</summary>
public sealed class PersistenceException : AppException
{
    public PersistenceException(string message) : base(message) { }

    public PersistenceException(string message, Exception? innerException) : base(message, innerException) { }
}