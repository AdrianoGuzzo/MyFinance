using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Exceptions;

namespace MyFinance.Desktop.Services;

public sealed record UserError(string Title, string Message, bool IsTechnical);

/// <summary>
/// Converte exceções em mensagens amigáveis, separando os tipos de erro:
/// validação, importação, persistência e técnico (inesperado).
/// </summary>
public static class ErrorMessages
{
    public static UserError Describe(Exception exception) => exception switch
    {
        ImportException e => new("Não foi possível importar o arquivo.", $"Motivo:\n{e.Message}", false),
        ValidationException e => new("Verifique os dados", e.Message, false),
        DomainException e => new("Verifique os dados", e.Message, false),
        PersistenceException e => new("Erro ao salvar", $"{e.Message}\nOs detalhes técnicos foram registrados no log.", false),
        _ => new(
            "Erro inesperado",
            "Ocorreu um erro inesperado e a operação não foi concluída.\nOs detalhes técnicos foram registrados no log.",
            true),
    };
}