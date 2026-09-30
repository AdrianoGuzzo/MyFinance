using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Exceptions;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;

using static MyFinance.Domain.Tests.TestData;

namespace MyFinance.Domain.Tests.Entities;

public sealed class ImportTests
{
    private static readonly Sha256Hash FileHash = Sha256Hash.Compute("arquivo");

    private static Import NewImport() =>
        Import.Start(@"C:\Users\fulano\Downloads\extrato.ofx", FileHash, ImportFileType.Ofx, Card.Id, Now);

    private static ImportTransaction AddEntry(Import import, decimal amount, string description, DuplicateCheck check) =>
        import.AddEntry(Day(1), amount, description, null,
            TransactionFingerprint.ComputeImportHash(Day(1), amount, description, null), "raw", check,
            amount < 0 ? TransactionKind.Purchase : TransactionKind.Refund, October.ReferenceMonth);

    private static readonly Dictionary<DateOnly, Invoice> Invoices = new() { [October.ReferenceMonth] = October };

    [Fact]
    public void Start_guarda_somente_o_nome_do_arquivo()
    {
        var import = NewImport();

        import.FileName.Should().Be("extrato.ofx");
        import.FileHash.Should().Be(FileHash);
        import.CreditCardId.Should().Be(Card.Id);
        import.Status.Should().Be(ImportStatus.Pending);
    }

    [Fact]
    public void Summary_conta_novas_duplicadas_e_erros()
    {
        var import = NewImport();
        AddEntry(import, -120.50m, "Supermercado", DuplicateCheck.NotDuplicate);
        AddEntry(import, -35.90m, "Uber", DuplicateCheck.NotDuplicate);
        AddEntry(import, -49.90m, "Netflix", new DuplicateCheck(DuplicateReason.ExternalId, Guid.CreateVersion7()));
        import.AddInvalidEntry("Data inválida.", "raw");

        import.Summary.Should().Be(new ImportSummary(Total: 4, New: 2, Duplicates: 1, Errors: 1));
        import.TransactionCount.Should().Be(4);
    }

    [Fact]
    public void Complete_cria_lancamentos_somente_para_itens_novos()
    {
        var import = NewImport();
        var fresh = AddEntry(import, -120.50m, "Supermercado", DuplicateCheck.NotDuplicate);
        var existingId = Guid.CreateVersion7();
        var duplicate = AddEntry(import, -49.90m, "Netflix", new DuplicateCheck(DuplicateReason.ExternalId, existingId));
        import.AddInvalidEntry("Valor inválido.", "raw");

        var created = import.Complete(Invoices, Now);

        created.Should().ContainSingle();
        var transaction = created[0];
        transaction.Amount.Should().Be(-120.50m);
        transaction.CreditCardId.Should().Be(Card.Id);
        transaction.InvoiceId.Should().Be(October.Id);
        transaction.Kind.Should().Be(TransactionKind.Purchase);
        transaction.ImportHash.Should().Be(fresh.ImportHash);

        fresh.Status.Should().Be(ImportTransactionStatus.Imported);
        fresh.TransactionId.Should().Be(transaction.Id);
        duplicate.Status.Should().Be(ImportTransactionStatus.Duplicate);
        duplicate.TransactionId.Should().Be(existingId);
        import.Status.Should().Be(ImportStatus.Completed);
        import.Summary.New.Should().Be(1);
    }

    [Fact]
    public void Mensagem_de_erro_longa_e_truncada_em_vez_de_impedir_a_importacao()
    {
        var import = NewImport();

        var entry = import.AddInvalidEntry(new string('x', 2000), "raw");

        entry.ErrorMessage!.Length.Should().Be(ImportTransaction.ErrorMessageMaxLength);
    }

    [Fact]
    public void Importacao_finalizada_nao_aceita_alteracoes()
    {
        var import = NewImport();
        import.Cancel();

        var act = () => import.Complete(Invoices, Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_sem_a_fatura_do_item_falha()
    {
        var import = NewImport();
        AddEntry(import, -10m, "Padaria", DuplicateCheck.NotDuplicate);

        var act = () => import.Complete(new Dictionary<DateOnly, Invoice>(), Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Start_sem_cartao_falha()
    {
        var act = () => Import.Start("extrato.ofx", FileHash, ImportFileType.Ofx, Guid.Empty, Now);

        act.Should().Throw<DomainException>();
    }
}