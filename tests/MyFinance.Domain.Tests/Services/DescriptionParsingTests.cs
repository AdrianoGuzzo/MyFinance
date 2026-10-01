using MyFinance.Domain.Enums;
using MyFinance.Domain.Services;

namespace MyFinance.Domain.Tests.Services;

public sealed class InstallmentParserTests
{
    [Theory]
    [InlineData("Notebook - Parcela 3/12", "Notebook", 3, 12)]
    [InlineData("NOTEBOOK PARC 03/12", "NOTEBOOK", 3, 12)]
    [InlineData("Loja X parc. 2 de 10", "Loja X", 2, 10)]
    [InlineData("Loja X - 2 de 10", "Loja X", 2, 10)]
    [InlineData("MAGAZINE LUIZA 01/10", "MAGAZINE LUIZA", 1, 10)]
    [InlineData("Geladeira Parcela 12/12", "Geladeira", 12, 12)]
    public void Reconhece_parcelas_no_fim_da_descricao(string description, string baseDescription, int number, int count)
    {
        InstallmentParser.Parse(description).Should().Be(new InstallmentInfo(baseDescription, number, count));
    }

    [Theory]
    [InlineData("Netflix.com")]
    [InlineData("Parcela 3/12")]            // sem descrição da compra
    [InlineData("Loja - Parcela 13/12")]    // parcela maior que o total
    [InlineData("Loja - Parcela 1/1")]      // "1x" não é parcelamento
    [InlineData("Loja - Parcela 0/5")]
    [InlineData("Loja 3/12")]               // sem dois dígitos e sem a palavra parcela: ambíguo
    [InlineData("Loja - Parcela 1/60")]     // acima do máximo
    [InlineData("")]
    [InlineData(null)]
    public void Ignora_descricoes_sem_parcela_valida(string? description)
    {
        InstallmentParser.Parse(description).Should().BeNull();
    }
}

public sealed class MerchantNormalizerTests
{
    [Theory]
    [InlineData("Netflix.com", "Netflix.com", "NETFLIX.COM")]
    [InlineData("NETFLIX.COM", "NETFLIX.COM", "NETFLIX.COM")]
    [InlineData("MP *MERCADOLIVRE", "MERCADOLIVRE", "MERCADOLIVRE")]
    [InlineData("PAG*JoseDaSilva", "JoseDaSilva", "JOSEDASILVA")]
    [InlineData("PAYPAL *STEAM GAMES", "STEAM GAMES", "STEAM GAMES")]
    [InlineData("Notebook Dell - Parcela 3/12", "Notebook Dell", "NOTEBOOK DELL")]
    [InlineData("Padaria Pão   Quente 1234567", "Padaria Pão Quente", "PADARIA PAO QUENTE")]
    [InlineData("Ifood *Restaurante", "Ifood *Restaurante", "IFOOD *RESTAURANTE")]
    public void Normaliza_o_estabelecimento(string description, string name, string key)
    {
        MerchantNormalizer.Normalize(description).Should().Be(new MerchantInfo(name, key));
    }

    [Fact]
    public void Descricao_so_com_ruido_mantem_o_original()
    {
        MerchantNormalizer.Normalize("MP*").Name.Should().Be("MP*");
    }
}

public sealed class TransactionKindClassifierTests
{
    [Theory]
    [InlineData(-89.90, "Ifood *Restaurante", TransactionKind.Purchase)]
    [InlineData(-8.40, "IOF COMPRA INTERNACIONAL", TransactionKind.Fee)]
    [InlineData(-35.00, "Anuidade Parc 1/12", TransactionKind.Fee)]
    [InlineData(-12.34, "Juros de rotativo", TransactionKind.Interest)]
    [InlineData(-5.00, "Multa por atraso", TransactionKind.Interest)]
    [InlineData(-50.00, "RIOFERTIL AGRO", TransactionKind.Purchase)]
    [InlineData(1500.00, "Pagamento recebido", TransactionKind.Payment)]
    [InlineData(1500.00, "PGTO DEBITO CONTA", TransactionKind.Payment)]
    [InlineData(89.90, "Estorno Ifood", TransactionKind.Refund)]
    [InlineData(10.00, "Crédito de cashback", TransactionKind.Refund)]
    public void Classifica_pelo_sinal_e_pela_descricao(decimal amount, string description, TransactionKind expected)
    {
        TransactionKindClassifier.Classify(amount, description).Should().Be(expected);
    }
}