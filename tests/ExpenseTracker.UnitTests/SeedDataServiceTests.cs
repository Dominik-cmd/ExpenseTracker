using BCrypt.Net;
using ExpenseTracker.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseTracker.UnitTests;

public sealed class SeedDataServiceTests
{
    [Fact]
    public async Task SeedAsync_ShouldCreateExpectedUserAndCategories()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = new SeedDataService(dbContext, NullLogger<SeedDataService>.Instance);

        await service.SeedAsync();

        var user = await dbContext.Users.SingleAsync(x => x.Username == "dominik");
        var categories = await dbContext.Categories.ToListAsync();
        var groceries = categories.Single(x => x.Name == "Groceries" && x.ParentCategoryId == null);

        BCrypt.Net.BCrypt.Verify(GetExpectedInitialPassword(), user.PasswordHash).Should().BeTrue();
        categories.Should().ContainSingle(x => x.Name == "Income" && x.ParentCategoryId == null && x.IsSystem);
        categories.Should().ContainSingle(x => x.Name == "Uncategorized" && x.ParentCategoryId == null && x.IsSystem);
        categories.Should().ContainSingle(x => x.Name == "Mercator" && x.ParentCategoryId == groceries.Id);
        categories.Should().ContainSingle(x => x.Name == "Misc Income" && x.ParentCategoryId != null);
        categories.Should().HaveCount(30);
        (await dbContext.LlmProviders.CountAsync()).Should().Be(3);
        (await dbContext.Settings.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SeedAsync_ShouldBeIdempotent()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = new SeedDataService(dbContext, NullLogger<SeedDataService>.Instance);

        await service.SeedAsync();
        await service.SeedAsync();

        (await dbContext.Users.CountAsync()).Should().Be(1);
        (await dbContext.Categories.CountAsync()).Should().Be(30);
        (await dbContext.LlmProviders.CountAsync()).Should().Be(3);
        (await dbContext.Settings.CountAsync()).Should().Be(2);
    }

    private static string GetExpectedInitialPassword()
        => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INITIAL_PASSWORD"))
            ? "ChangeMeNow!"
            : Environment.GetEnvironmentVariable("INITIAL_PASSWORD")!;
}
