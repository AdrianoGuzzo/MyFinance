using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;
using MyFinance.Infrastructure.Persistence;
using MyFinance.Infrastructure.Persistence.Queries;
using MyFinance.Infrastructure.Persistence.Repositories;

namespace MyFinance.Infrastructure.Tests.Persistence;

public sealed class PersistenceTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private SqliteTestDatabase _database = null!;

    public async ValueTask InitializeAsync() => _database = await SqliteTestDatabase.CreateAsync(_ct);

    public ValueTask DisposeAsync() => _database.DisposeAsync();

    private static DateOnly Day(int day, int month = 9) => new(2026, month, day);

    private static CreditCard NewCard(string lastFour = "4321") => CreditCard.Create(
        "Nubank", "Nu Pagamentos", CardBrand.Mastercard, LastFourDigits.Create(lastFour), 5000m,
        DayOfMonth.Create(3), DayOfMonth.Create(10), Now);

    /// <summary>Cartão (fecha dia 3) e sua fatura de outubro/2026, já gravados.</summary>
    private async Task<(CreditCard Card, Invoice Invoice)> SaveCardAsync(string lastFour = "4321")
    {
        var card = NewCard(lastFour);
        var invoice = Invoice.For(card, card.GetInvoicePeriodForMonth(Day(1, 10)));
        await using var db = _database.CreateContext();
        db.AddRange(card, invoice);
        await db.SaveChangesAsync(_ct);
        return (card, invoice);
    }

    private static Transaction Purchase(Invoice invoice, DateOnly date, decimal amount, string description, string? externalId = null) =>
        Transaction.Create(invoice, date, amount, description, amount < 0 ? TransactionKind.Purchase : TransactionKind.Refund, Now, externalId);

    [Fact]
    public async Task Migrations_estao_sincronizadas_com_o_modelo()
    {
        await using var db = _database.CreateContext();

        (await db.Database.GetPendingMigrationsAsync(_ct)).Should().BeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse("toda mudança de modelo precisa de uma migration");
    }

    [Theory]
    [InlineData("Transactions", "IX_Transactions_CreditCardId_Date")]
    [InlineData("Transactions", "IX_Transactions_InvoiceId")]
    [InlineData("Transactions", "IX_Transactions_Date")]
    [InlineData("Transactions", "IX_Transactions_ExternalId")]
    [InlineData("Transactions", "IX_Transactions_CategoryId")]
    [InlineData("Transactions", "IX_Transactions_ImportHash")]
    [InlineData("Transactions", "IX_Transactions_MerchantKey")]
    [InlineData("Transactions", "IX_Transactions_InstallmentPurchaseId")]
    [InlineData("Invoices", "IX_Invoices_CreditCardId_ReferenceMonth")]
    [InlineData("InstallmentPurchases", "IX_InstallmentPurchases_CreditCardId_MerchantKey")]
    [InlineData("CategoryRules", "IX_CategoryRules_IsActive_Priority")]
    [InlineData("SpendingLimits", "IX_SpendingLimits_CategoryId")]
    [InlineData("RecurringExpenses", "IX_RecurringExpenses_MerchantKey")]
    [InlineData("Imports", "IX_Imports_FileHash")]
    public async Task Indices_existem_no_banco(string table, string index)
    {
        await using var db = _database.CreateContext();

        var indexes = await db.Database
            .SqlQuery<string>($"SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = {table}")
            .ToListAsync(_ct);

        indexes.Should().Contain(index);
    }

    [Fact]
    public async Task Tabelas_de_conta_bancaria_nao_existem()
    {
        await using var db = _database.CreateContext();

        var tables = await db.Database
            .SqlQuery<string>($"SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync(_ct);

        tables.Should().NotContain("Accounts");
        tables.Should().Contain(["CreditCards", "Invoices", "Transactions", "InstallmentPurchases", "Categories", "CategoryRules",
            "SpendingLimits", "FinancialGoals", "RecurringExpenses", "Imports", "ImportTransactions"]);
    }

    [Fact]
    public async Task Cartao_fatura_e_categoria_com_subcategoria_sobrevivem_ida_e_volta()
    {
        var (card, invoice) = await SaveCardAsync();
        var food = Category.Create("Alimentação", HexColor.Create("#E67E22"));
        var delivery = food.CreateSubcategory("Delivery");
        invoice.MarkPaid(Now);

        await using (var db = _database.CreateContext())
        {
            db.AddRange(food, delivery);
            db.Update(invoice);
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        (await read.CreditCards.SingleAsync(_ct)).Should().BeEquivalentTo(card);
        (await read.Invoices.SingleAsync(_ct)).Should().BeEquivalentTo(invoice);
        (await read.Categories.SingleAsync(c => c.Id == delivery.Id, _ct)).Should().BeEquivalentTo(delivery);
    }

    [Fact]
    public async Task Fatura_duplicada_para_o_mesmo_cartao_e_mes_e_rejeitada()
    {
        var (card, _) = await SaveCardAsync();
        await using var db = _database.CreateContext();
        db.Invoices.Add(Invoice.For(card, card.GetInvoicePeriodForMonth(Day(1, 10))));

        var act = () => new UnitOfWork(db, NullLogger<UnitOfWork>.Instance).SaveChangesAsync(_ct);

        await act.Should().ThrowAsync<PersistenceException>();
    }

    [Fact]
    public async Task Parcelamento_regras_limite_meta_e_recorrente_sobrevivem_ida_e_volta()
    {
        var (card, invoice) = await SaveCardAsync();
        var category = Category.Create("Lazer");
        var purchase = InstallmentPurchase.Create(card.Id, "Notebook", 500m, 12, Day(1, 10), Now);
        var transaction = Purchase(invoice, Day(10), -500m, "Notebook - Parcela 1/12");
        transaction.LinkInstallment(purchase, 1);
        var rule = CategoryRule.Create("cinema", category, 5, Now);
        var limit = SpendingLimit.Create(category, 500m, Now);
        var goal = FinancialGoal.Create("Economizar", 1000m, Day(1), Now);
        var recurring = RecurringExpense.Create("NETFLIX.COM", "Netflix.com", 55.90m, category.Id, Day(1), Now);
        recurring.Classify(RecurringClassification.Optional, Now);

        await using (var db = _database.CreateContext())
        {
            db.AddRange(category, purchase, transaction, rule, limit, goal, recurring);
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        (await read.InstallmentPurchases.SingleAsync(_ct)).Should().BeEquivalentTo(purchase);
        (await read.Transactions.SingleAsync(_ct)).Should().BeEquivalentTo(transaction);
        (await read.CategoryRules.SingleAsync(_ct)).Should().BeEquivalentTo(rule);
        (await read.SpendingLimits.SingleAsync(_ct)).Should().BeEquivalentTo(limit);
        (await read.FinancialGoals.SingleAsync(_ct)).Should().BeEquivalentTo(goal);
        (await read.RecurringExpenses.SingleAsync(_ct)).Should().BeEquivalentTo(recurring);
    }

    [Fact]
    public async Task Importacao_persiste_agregado_com_itens_e_lancamentos()
    {
        var (card, invoice) = await SaveCardAsync();
        var import = Import.Start("fatura.ofx", Sha256Hash.Compute("arquivo"), ImportFileType.Ofx, card.Id, Now);
        import.AddEntry(Day(29), -120.50m, "Supermercado", "FIT-1",
            TransactionFingerprint.ComputeImportHash(Day(29), -120.50m, "Supermercado", null), "<STMTTRN>...", DuplicateCheck.NotDuplicate,
            TransactionKind.Purchase, invoice.ReferenceMonth);
        import.AddInvalidEntry("Data inválida.", "linha 3");
        var created = import.Complete(new Dictionary<DateOnly, Invoice> { [invoice.ReferenceMonth] = invoice }, Now);

        await using (var db = _database.CreateContext())
        {
            db.Imports.Add(import);
            db.Transactions.AddRange(created);
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        var loaded = await read.Imports.Include(i => i.Transactions).SingleAsync(_ct);
        loaded.Summary.Should().Be(new ImportSummary(Total: 2, New: 1, Duplicates: 0, Errors: 1));
        loaded.Transactions.Should().Contain(t => t.TransactionId == created[0].Id && t.Status == ImportTransactionStatus.Imported
            && t.Kind == TransactionKind.Purchase && t.InvoiceMonth == invoice.ReferenceMonth);
        (await read.Transactions.SingleAsync(_ct)).ImportHash.Should().Be(created[0].ImportHash);
    }

    [Fact]
    public async Task Soma_ordenacao_e_comparacao_de_decimal_sao_exatas_no_sqlite()
    {
        var (_, invoice) = await SaveCardAsync();
        await using (var db = _database.CreateContext())
        {
            foreach (var amount in new[] { 0.1m, 0.2m, -10m, 9m, 100.55m })
            {
                db.Transactions.Add(Purchase(invoice, Day(10), amount, "x"));
            }

            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        (await read.Transactions.SumAsync(t => t.Amount, _ct)).Should().Be(99.85m);
        (await read.Transactions.OrderBy(t => t.Amount).Select(t => t.Amount).ToListAsync(_ct))
            .Should().Equal(-10m, 0.1m, 0.2m, 9m, 100.55m);
        (await read.Transactions.CountAsync(t => t.Amount < 0, _ct)).Should().Be(1);
    }

    [Fact]
    public async Task Candidatos_a_duplicidade_filtram_por_cartao_periodo_e_ExternalId()
    {
        var (card, invoice) = await SaveCardAsync();
        var (_, otherInvoice) = await SaveCardAsync("9999");
        var inRange = Purchase(invoice, Day(10), -1m, "No período");
        var outOfRangeSameFitId = Purchase(invoice, Day(1, 1), -2m, "Fora do período, mesmo FITID", "FIT-9");
        var outOfRange = Purchase(invoice, Day(1, 1), -3m, "Fora do período");
        var otherCard = Purchase(otherInvoice, Day(10), -4m, "Outro cartão");

        await using (var db = _database.CreateContext())
        {
            db.Transactions.AddRange(inRange, outOfRangeSameFitId, outOfRange, otherCard);
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        var result = await new TransactionRepository(read)
            .GetForDuplicateCheckAsync(card.Id, Day(1), Day(30), ["FIT-9"], _ct);

        result.Select(r => r.Id).Should().BeEquivalentTo([inRange.Id, outOfRangeSameFitId.Id]);
    }

    [Fact]
    public async Task Consulta_de_gastos_traz_a_competencia_da_fatura()
    {
        var (card, invoice) = await SaveCardAsync();
        await using (var db = _database.CreateContext())
        {
            db.Transactions.AddRange(Purchase(invoice, Day(10), -100m, "Mercado"), Purchase(invoice, Day(11), 30m, "Estorno"));
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        var queries = new SpendingQueries(read);
        var entries = await queries.GetEntriesAsync(Day(1, 10), Day(1, 10), card.Id, _ct);

        entries.Select(e => (e.InvoiceMonth, e.Spending)).Should().BeEquivalentTo([(Day(1, 10), 100m), (Day(1, 10), -30m)]);
        (await queries.GetEntriesAsync(Day(1, 11), Day(1, 12), null, _ct)).Should().BeEmpty();
        (await queries.GetFirstMonthAsync(_ct)).Should().Be(Day(1, 10));
    }

    [Fact]
    public async Task Busca_importacao_concluida_pelo_hash_do_arquivo()
    {
        var (card, _) = await SaveCardAsync();
        var hash = Sha256Hash.Compute("arquivo");
        var none = new Dictionary<DateOnly, Invoice>();
        var older = Import.Start("a.ofx", hash, ImportFileType.Ofx, card.Id, Now.AddDays(-2));
        older.Complete(none, Now);
        var latest = Import.Start("b.ofx", hash, ImportFileType.Ofx, card.Id, Now);
        latest.Complete(none, Now);
        var cancelled = Import.Start("c.ofx", hash, ImportFileType.Ofx, card.Id, Now.AddDays(1));
        cancelled.Cancel();

        await using (var db = _database.CreateContext())
        {
            db.Imports.AddRange(older, latest, cancelled);
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        var repository = new ImportRepository(read);
        (await repository.FindLatestCompletedByFileHashAsync(hash, _ct))!.Id.Should().Be(latest.Id);
        (await repository.FindLatestCompletedByFileHashAsync(Sha256Hash.Compute("outro"), _ct)).Should().BeNull();
    }

    [Theory]
    [InlineData("saude", true)]
    [InlineData("SAÚDE", true)]
    [InlineData("Saúde Mental", false)]
    public async Task Nome_de_categoria_repetido_ignora_caixa_e_acentos(string name, bool expected)
    {
        await using (var db = _database.CreateContext())
        {
            db.Categories.Add(Category.Create("Saúde"));
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        (await new CategoryRepository(read).NameExistsAsync(name, null, null, _ct)).Should().Be(expected);
    }

    [Fact]
    public async Task Banco_rejeita_tipo_de_lancamento_invalido()
    {
        var (card, invoice) = await SaveCardAsync();
        await using var db = _database.CreateContext();

        // Salvaguarda além do domínio: protege contra escrita por SQL direto ou migrations futuras.
        var invalidKind = () => db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO Transactions (Id, CreditCardId, InvoiceId, Date, Amount, Description, MerchantName, MerchantKey, Kind, CreatedAt)
            VALUES ({Guid.CreateVersion7()}, {card.Id}, {invoice.Id}, '2026-09-01', '-1.0', 'x', 'x', 'X', 99, '2026-09-30 00:00:00')
            """, _ct);

        (await invalidKind.Should().ThrowAsync<SqliteException>()).WithMessage("*CK_Transactions_Kind*");
    }

    [Fact]
    public async Task Falha_de_integridade_vira_PersistenceException()
    {
        // Lançamento apontando para fatura e cartão inexistentes viola as FKs.
        var orphanCard = NewCard();
        var orphan = Purchase(Invoice.For(orphanCard, orphanCard.GetInvoicePeriodForMonth(Day(1, 10))), Day(10), -1m, "x");
        await using var db = _database.CreateContext();
        db.Transactions.Add(orphan);

        var act = () => new UnitOfWork(db, NullLogger<UnitOfWork>.Instance).SaveChangesAsync(_ct);

        (await act.Should().ThrowAsync<PersistenceException>()).WithInnerException<DbUpdateException>();
    }
}