using MyFinance.Application.Accounts;
using MyFinance.Application.Common.Exceptions;
using MyFinance.Application.CreditCards;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.ValueObjects;

namespace MyFinance.Application.Tests.Accounts;

public sealed class AccountServiceTests : ApplicationTestBase
{
    private AccountService Service => Host.Get<AccountService>();

    [Fact]
    public async Task CreateAccount_cria_conta_Nubank_com_saldo_calculado()
    {
        var id = await CreateAccountAsync(initialBalance: 1000m);
        var owner = TransactionOwner.ForAccount(id);
        await AddTransactionAsync(owner, Day(5), 5000m, "Salário");
        await AddTransactionAsync(owner, Day(6), -120.50m, "Mercado");

        var account = (await Service.ListAsync(includeInactive: false, Ct)).Single();

        account.Should().BeEquivalentTo(new
        {
            Id = id,
            Name = "Nubank",
            BankName = "Nu Pagamentos",
            AccountType = AccountType.Payment,
            Agency = "0001",
            InitialBalance = 1000m,
            Balance = 5879.50m,
            IsActive = true,
        });
        account.AccountNumber!.Value.Should().Be("99999999-9");
        account.ToString().Should().NotContain("99999999", "o número da conta é mascarado ao virar texto");
    }

    [Fact]
    public async Task CreateAccount_com_dados_invalidos_gera_erro_de_validacao()
    {
        var invalidNumber = () => Service.CreateAsync(new SaveAccountCommand("Conta", "Banco", AccountType.Checking, 0m, "12AB"), Ct);
        var missingName = () => Service.CreateAsync(new SaveAccountCommand(" ", "Banco", AccountType.Checking, 0m), Ct);

        await invalidNumber.Should().ThrowAsync<DomainException>().WithMessage("Número de conta inválido*");
        await missingName.Should().ThrowAsync<DomainException>().WithMessage("O nome da conta é obrigatório.");
    }

    [Fact]
    public async Task Update_e_Deactivate_alteram_a_conta()
    {
        var id = await CreateAccountAsync();

        await Service.UpdateAsync(id, new SaveAccountCommand("Nubank PJ", "Nu", AccountType.Checking, 10m), Ct);
        await Service.DeactivateAsync(id, Ct);

        (await Service.ListAsync(includeInactive: false, Ct)).Should().BeEmpty();
        var account = (await Service.ListAsync(includeInactive: true, Ct)).Single();
        account.Name.Should().Be("Nubank PJ");
        account.AccountNumber.Should().BeNull();
        account.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Operacao_em_conta_inexistente_gera_erro_de_validacao()
    {
        var act = () => Service.DeactivateAsync(Guid.CreateVersion7(), Ct);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("Conta não encontrada.");
    }
}

public sealed class CreditCardServiceTests : ApplicationTestBase
{
    [Fact]
    public async Task Lista_cartao_com_fatura_atual_contendo_somente_compras_do_periodo()
    {
        // Hoje: 30/09/2026; fechamento dia 3 → fatura atual: 03/09 a 02/10, vence 10/10.
        var id = await CreateCardAsync(closingDay: 3, dueDay: 10);
        var owner = TransactionOwner.ForCreditCard(id);
        await AddTransactionAsync(owner, Day(2), -100m, "Fatura anterior");
        await AddTransactionAsync(owner, Day(3), -50m, "Uber");
        await AddTransactionAsync(owner, Day(25), -49.90m, "Netflix");
        await AddTransactionAsync(owner, Day(20), 800m, "Pagamento recebido");

        var card = (await Host.Get<CreditCardService>().ListAsync(false, Ct)).Single();

        card.LastFourDigits.Should().Be("1234");
        card.CurrentInvoice.Should().Be(new InvoiceDto(Day(3), Day(3, 10), Day(10, 10), Day(1, 10), 99.90m));
    }

    [Fact]
    public async Task CreateCard_com_dia_invalido_falha()
    {
        var act = () => Host.Get<CreditCardService>().CreateAsync(new SaveCreditCardCommand("Cartão", "Banco", "1234", 0m, 32, 10), Ct);

        await act.Should().ThrowAsync<DomainException>().WithMessage("O dia deve estar entre 1 e 31.");
    }
}