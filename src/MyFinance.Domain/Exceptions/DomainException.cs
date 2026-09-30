namespace MyFinance.Domain.Exceptions;

/// <summary>
/// Violação de uma regra de negócio. A mensagem é segura para exibição ao usuário.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException() { }

    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}