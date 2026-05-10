using System.Text;
using ExpenseTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ExpenseTracker.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RawMessage> RawMessages => Set<RawMessage>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MerchantRule> MerchantRules => Set<MerchantRule>();
    public DbSet<LlmProvider> LlmProviders => Set<LlmProvider>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<LlmCallLog> LlmCallLogs => Set<LlmCallLog>();
    public DbSet<Summary> Summaries => Set<Summary>();
    public DbSet<InvestmentProvider> InvestmentProviders => Set<InvestmentProvider>();
    public DbSet<InvestmentAccount> InvestmentAccounts => Set<InvestmentAccount>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<InvestmentTransaction> InvestmentTransactions => Set<InvestmentTransaction>();
    public DbSet<ManualAccountBalance> ManualAccountBalances => Set<ManualAccountBalance>();
    public DbSet<ManualBalanceHistory> ManualBalanceHistories => Set<ManualBalanceHistory>();
    public DbSet<PortfolioHistory> PortfolioHistories => Set<PortfolioHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySnakeCaseNamingConvention(modelBuilder);
    }

    private static void ApplySnakeCaseNamingConvention(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character))
            {
                if (index > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
