using ExpenseTracker.Application.Models;
using ExpenseTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace ExpenseTracker.IntegrationTests;

public sealed class TransactionTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
  [Fact]
  public async Task GetTransactions_ReturnsPagedResults()
  {
    var groceriesId = await GetCategoryIdAsync("Groceries");
    await SeedTransactionAsync(groceriesId, 10.00m, "MERCATOR", DateTime.UtcNow.AddDays(-1));
    await SeedTransactionAsync(groceriesId, 20.00m, "SPAR", DateTime.UtcNow.AddDays(-2));
    var client = await CreateAuthenticatedClientAsync();

    var response = await client.GetAsync("/api/transactions?page=1&pageSize=1");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var payload = await ReadAsAsync<PagedResult<TransactionDto>>(response);
    Assert.NotNull(payload);
    Assert.Equal(2, payload!.TotalCount);
    Assert.Equal(1, payload.Page);
    Assert.Equal(1, payload.PageSize);
    Assert.Single(payload.Items);
  }

  [Fact]
  public async Task CreateTransaction_CreatesManualTransaction()
  {
    var client = await CreateAuthenticatedClientAsync();
    var groceriesId = await GetCategoryIdAsync("Groceries");

    var response = await client.PostAsJsonAsync(
        "/api/transactions",
        new CreateTransactionRequest(
            23.45m,
            Direction.Debit,
            TransactionType.Purchase,
            new DateTime(2024, 3, 22, 14, 35, 0, DateTimeKind.Utc),
            "MERCATOR MARIBOR SI",
            groceriesId,
            "Manual entry"),
        CustomWebApplicationFactory.JsonOptions);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var payload = await ReadAsAsync<TransactionDto>(response);
    Assert.NotNull(payload);
    Assert.Equal(23.45m, payload!.Amount);
    Assert.Equal("MERCATOR", payload.MerchantNormalized);
    Assert.Equal(CategorySource.Manual, payload.CategorySource);
    Assert.Equal(TransactionSource.Manual, payload.TransactionSource);

    var transactionCount = await Factory.ExecuteDbContextAsync(db => db.Transactions.CountAsync());
    Assert.Equal(1, transactionCount);
  }

  [Fact]
  public async Task UpdateTransaction_PatchesExistingTransaction()
  {
    var groceriesId = await GetCategoryIdAsync("Groceries");
    var fuelId = await GetCategoryIdAsync("Fuel");
    var transactionId = await SeedTransactionAsync(groceriesId, 12.34m, "MERCATOR", DateTime.UtcNow.AddDays(-1));
    var client = await CreateAuthenticatedClientAsync();

    var response = await client.PatchAsJsonAsync(
        $"/api/transactions/{transactionId}",
        new UpdateTransactionRequest(
            99.50m,
            "usd",
            Direction.Debit,
            TransactionType.TransferOut,
            new DateTime(2024, 3, 25, 0, 0, 0, DateTimeKind.Utc),
            "PETROL MARIBOR SI",
            fuelId,
            "Updated transaction"),
        CustomWebApplicationFactory.JsonOptions);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var payload = await ReadAsAsync<TransactionDto>(response);
    Assert.NotNull(payload);
    Assert.Equal(99.50m, payload!.Amount);
    Assert.Equal("USD", payload.Currency);
    Assert.Equal("PETROL", payload.MerchantNormalized);
    Assert.Equal(fuelId, payload.CategoryId);
    Assert.Equal("Updated transaction", payload.Notes);
  }

  [Fact]
  public async Task DeleteTransaction_SoftDeletesTransaction()
  {
    var groceriesId = await GetCategoryIdAsync("Groceries");
    var transactionId = await SeedTransactionAsync(groceriesId, 12.34m, "MERCATOR", DateTime.UtcNow.AddDays(-1));
    var client = await CreateAuthenticatedClientAsync();

    var response = await client.DeleteAsync($"/api/transactions/{transactionId}");

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var isDeleted = await Factory.ExecuteDbContextAsync(db =>
        db.Transactions.Where(x => x.Id == transactionId)
            .Select(x => x.IsDeleted)
            .SingleAsync());

    Assert.True(isDeleted);
  }
}
