using System.Net.Http.Json;
using ExpenseTracker.Core.Entities;
using ExpenseTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.IntegrationTests;

public abstract class IntegrationTestBase(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected CustomWebApplicationFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected Task<HttpClient> CreateAuthenticatedClientAsync() => Factory.CreateAuthenticatedClientAsync();

    protected Task<Guid> GetUserIdAsync() => Factory.ExecuteDbContextAsync(db =>
        db.Users.Where(x => x.Username == CustomWebApplicationFactory.TestUsername)
            .Select(x => x.Id)
            .SingleAsync());

    protected Task<Guid> GetCategoryIdAsync(string name, Guid? parentCategoryId = null) => Factory.ExecuteDbContextAsync(db =>
        db.Categories.Where(x => x.Name == name && x.ParentCategoryId == parentCategoryId)
            .Select(x => x.Id)
            .SingleAsync());

    protected async Task<Guid> SeedTransactionAsync(
        Guid categoryId,
        decimal amount,
        string merchantNormalized,
        DateTime transactionDate,
        Direction direction = Direction.Debit,
        TransactionType transactionType = TransactionType.Purchase,
        string? notes = null)
    {
        var userId = await GetUserIdAsync();
        var transactionId = Guid.NewGuid();

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Transactions.Add(new Transaction
            {
                Id = transactionId,
                UserId = userId,
                CategoryId = categoryId,
                Amount = amount,
                Currency = "EUR",
                Direction = direction,
                TransactionType = transactionType,
                TransactionDate = transactionDate,
                MerchantRaw = merchantNormalized,
                MerchantNormalized = merchantNormalized,
                Notes = notes,
                CategorySource = CategorySource.Manual,
                TransactionSource = TransactionSource.Manual,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        });

        return transactionId;
    }

    protected async Task<T?> ReadAsAsync<T>(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<T>(CustomWebApplicationFactory.JsonOptions);
}
