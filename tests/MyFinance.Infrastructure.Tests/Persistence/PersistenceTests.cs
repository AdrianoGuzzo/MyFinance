using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using MyFinance.Application.Common.Exceptions;
using MyFinance.Domain.Entities;
using MyFinance.Domain.Enums;
using MyFinance.Domain.Services;
using MyFinance.Domain.ValueObjects;
using MyFinance.Infrastructure.Persistence;
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

    private async Task<Account> SaveAccountAsync()
    {
        var account = Account.Create("Nubank", "Nu Pagamentos", AccountType.Payment, 1000m, Now,
            AccountNumber.Create("12345678-9"), "0001");
        await using var db = _database.CreateContext();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(_ct);
        return account;
    }

    [Fact]
    public async Task Migrations_estao_sincronizadas_com_o_modelo()
    {
        await using var db = _database.CreateContext();

        (await db.Database.GetPendingMigrationsAsync(_ct)).Should().BeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse("toda mudança de modelo precisa de uma migration");
    }

    [Theory]
    [InlineData("Transactions", "IX_Transactions_AccountId_Date")]
    [InlineData("Transactions", "IX_Transactions_CreditCardId_Date")]
    [InlineData("Transactions", "IX_Transactions_Date")]
    [InlineData("Transactions", "IX_Transactions_ExternalId")]
    [InlineData("Transactions", "IX_Transactions_CategoryId")]
    [InlineData("Transactions", "IX_Transactions_ImportHash")]
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
    public async Task Conta_e_value_objects_sobrevivem_ida_e_volta()
    {
        var account = await SaveAccountAsync();

        await using var db = _database.CreateContext();
        var loaded = await db.Accounts.SingleAsync(_ct);

        loaded.Should().BeEquivalentTo(account);
        loaded.AccountNumber!.Value.Should().Be("12345678-9");
        loaded.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Cartao_e_categoria_com_subcategoria_sobrevivem_ida_e_volta()
    {
        var card = CreditCard.Create("Nubank", "Nu", LastFourDigits.Create("4321"), 5000m,
            DayOfMonth.Create(3), DayOfMonth.Create(10), Now);
        var food = Category.Create("Alimentação", CategoryType.Expense, HexColor.Create("#E67E22"));
        var delivery = food.CreateSubcategory("Delivery");

        await using (var db = _database.CreateContext())
        {
            db.AddRange(card, food, delivery);
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        (await read.CreditCards.SingleAsync(_ct)).Should().BeEquivalentTo(card);
        (await read.Categories.SingleAsync(c => c.Id == delivery.Id, _ct)).Should().BeEquivalentTo(delivery);
    }

    [Fact]
    public async Task Importacao_persiste_agregado_com_itens_e_lancamentos()
    {
        var account = await SaveAccountAsync();
        var owner = TransactionOwner.ForAccount(account.Id);
        var import = Import.Start("extrato.ofx", Sha256Hash.Compute("arquivo"), ImportFileType.Ofx, owner, Now);
        import.AddEntry(Day(29), -120.50m, "Supermercado", "FIT-1",
            TransactionFingerprint.ComputeImportHash(Day(29), -120.50m, "Supermercado", null), "<STMTTRN>...", DuplicateCheck.NotDuplicate);
        import.AddInvalidEntry("Data inválida.", "linha 3");
        var created = import.Complete(Now);

        await using (var db = _database.CreateContext())
        {
            db.Imports.Add(import);
            db.Transactions.AddRange(created);
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        var loaded = await read.Imports.Include(i => i.Transactions).SingleAsync(_ct);
        loaded.Summary.Should().Be(new ImportSummary(Total: 2, New: 1, Duplicates: 0, Errors: 1));
        loaded.Transactions.Should().Contain(t => t.TransactionId == created[0].Id && t.Status == ImportTransactionStatus.Imported);
        (await read.Transactions.SingleAsync(_ct)).ImportHash.Should().Be(created[0].ImportHash);
    }

    [Fact]
    public async Task Soma_ordenacao_e_comparacao_de_decimal_sao_exatas_no_sqlite()
    {
        var account = await SaveAccountAsync();
        var owner = TransactionOwner.ForAccount(account.Id);
        await using (var db = _database.CreateContext())
        {
            foreach (var amount in new[] { 0.1m, 0.2m, -10m, 9m, 100.55m })
            {
                db.Transactions.Add(Transaction.Create(owner, Day(1), amount, "x", Now));
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
    public async Task Candidatos_a_duplicidade_filtram_por_dono_periodo_e_ExternalId()
    {
        var account = await SaveAccountAsync();
        var other = Account.Create("Outra", "Banco", AccountType.Checking, 0m, Now);
        var owner = TransactionOwner.ForAccount(account.Id);
        var inRange = Transaction.Create(owner, Day(10), -1m, "No período", Now);
        var outOfRangeSameFitId = Transaction.Create(owner, Day(1, 1), -2m, "Fora do período, mesmo FITID", Now, "FIT-9");
        var outOfRange = Transaction.Create(owner, Day(1, 1), -3m, "Fora do período", Now);
        var otherAccount = Transaction.Create(TransactionOwner.ForAccount(other.Id), Day(10), -4m, "Outra conta", Now);

        await using (var db = _database.CreateContext())
        {
            db.Accounts.Add(other);
            db.Transactions.AddRange(inRange, outOfRangeSameFitId, outOfRange, otherAccount);
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        var result = await new TransactionRepository(read)
            .GetForDuplicateCheckAsync(owner, Day(1), Day(30), ["FIT-9"], _ct);

        result.Select(r => r.Id).Should().BeEquivalentTo([inRange.Id, outOfRangeSameFitId.Id]);
    }

    [Fact]
    public async Task Busca_importacao_concluida_pelo_hash_do_arquivo()
    {
        var account = await SaveAccountAsync();
        var hash = Sha256Hash.Compute("arquivo");
        var owner = TransactionOwner.ForAccount(account.Id);
        var older = Import.Start("a.ofx", hash, ImportFileType.Ofx, owner, Now.AddDays(-2));
        older.Complete(Now);
        var latest = Import.Start("b.ofx", hash, ImportFileType.Ofx, owner, Now);
        latest.Complete(Now);
        var cancelled = Import.Start("c.ofx", hash, ImportFileType.Ofx, owner, Now.AddDays(1));
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
            db.Categories.Add(Category.Create("Saúde", CategoryType.Expense));
            await db.SaveChangesAsync(_ct);
        }

        await using var read = _database.CreateContext();
        (await new CategoryRepository(read).NameExistsAsync(name, null, null, _ct)).Should().Be(expected);
    }

    [Fact]
    public async Task Banco_rejeita_lancamento_sem_dono_ou_com_dois_donos()
    {
        var account = await SaveAccountAsync();
        var card = CreditCard.Create("Cartão", "Banco", LastFourDigits.Create("1111"), 0m,
            DayOfMonth.Create(1), DayOfMonth.Create(10), Now);
        await using var db = _database.CreateContext();
        db.CreditCards.Add(card);
        await db.SaveChangesAsync(_ct);

        // Salvaguarda além do domínio: protege contra escrita por SQL direto ou migrations futuras.
        var bothOwners = () => db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO Transactions (Id, AccountId, CreditCardId, Date, Amount, Description, TransactionType, CreatedAt)
            VALUES ({Guid.CreateVersion7()}, {account.Id}, {card.Id}, '2026-09-01', '-1.0', 'x', 2, '2026-09-30 00:00:00')
            """, _ct);

        (await bothOwners.Should().ThrowAsync<Microsoft.Data.Sqlite.SqliteException>()).WithMessage("*CK_Transactions_SingleOwner*");
    }

    [Fact]
    public async Task Falha_de_integridade_vira_PersistenceException()
    {
        // Lançamento apontando para conta inexistente viola a FK.
        var orphan = Transaction.Create(TransactionOwner.ForAccount(Guid.CreateVersion7()), Day(1), -1m, "x", Now);
        await using var db = _database.CreateContext();
        db.Transactions.Add(orphan);

        var act = () => new UnitOfWork(db, NullLogger<UnitOfWork>.Instance).SaveChangesAsync(_ct);

        (await act.Should().ThrowAsync<PersistenceException>()).WithInnerException<DbUpdateException>();
    }
}