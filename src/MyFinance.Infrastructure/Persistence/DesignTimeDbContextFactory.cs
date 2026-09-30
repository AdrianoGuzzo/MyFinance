using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyFinance.Infrastructure.Persistence;

/// <summary>Usada somente pelo <c>dotnet ef</c> para gerar migrations.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(SqliteConnectionFactory.BuildConnectionString(Path.Combine(Path.GetTempPath(), "myfinance-design.db")))
            .Options;

        return new FinanceDbContext(options);
    }
}