using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.Imports;
using MyFinance.Infrastructure.Imports.CSV;
using MyFinance.Infrastructure.Imports.OFX;

namespace MyFinance.Application.Tests.Imports;

public sealed class TransactionImporterResolverTests
{
    private readonly TransactionImporterResolver _resolver = new([new OfxTransactionImporter(), new CsvTransactionImporter()]);

    [Theory]
    [InlineData("extrato-setembro.ofx", typeof(OfxTransactionImporter))]
    [InlineData(@"C:\Downloads\Nubank_2026-09-30.csv", typeof(CsvTransactionImporter))]
    public void Resolve_escolhe_importador_pela_extensao(string fileName, Type expected) =>
        _resolver.Resolve(fileName).Should().BeOfType(expected);

    [Theory]
    [InlineData("extrato.pdf")]
    [InlineData("extrato")]
    public void Formato_desconhecido_gera_mensagem_amigavel(string fileName)
    {
        var act = () => _resolver.Resolve(fileName);

        act.Should().Throw<ImportException>().WithMessage("O formato do arquivo não é reconhecido.*");
    }
}