using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ExpenseTracker.Api.Controllers;
using ExpenseTracker.Api.Models;
using ExpenseTracker.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseTracker.UnitTests;

public sealed class CategoryResolutionTests
{
    [Fact]
    public async Task CreateAsync_ShouldAllowSecondLevelCategory()
    {
        using var dbContext = TestDbContextFactory.Create();
        var parent = new Category { Name = "Groceries", SortOrder = 1 };
        dbContext.Categories.Add(parent);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.CreateAsync(new CreateCategoryRequest("Organic", "#00ff00", "leaf", parent.Id), CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        var category = createdResult.Value.Should().BeOfType<CategoryDto>().Subject;
        category.Name.Should().Be("Organic");
        category.ParentCategoryId.Should().Be(parent.Id);
        category.IsSystem.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectThirdLevelCategory()
    {
        using var dbContext = TestDbContextFactory.Create();
        var parent = new Category { Name = "Groceries", SortOrder = 1 };
        var child = new Category { Name = "Mercator", ParentCategoryId = parent.Id, SortOrder = 1 };
        dbContext.Categories.AddRange(parent, child);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.CreateAsync(new CreateCategoryRequest("Corner Shop", null, null, child.Id), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateAsync_ShouldForbidSystemCategory()
    {
        using var dbContext = TestDbContextFactory.Create();
        var income = new Category { Name = "Income", IsSystem = true, SortOrder = 19 };
        dbContext.Categories.Add(income);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.UpdateAsync(income.Id, new UpdateCategoryRequest("Salary", null, null, null), CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldForbidSystemCategory()
    {
        using var dbContext = TestDbContextFactory.Create();
        var income = new Category { Name = "Income", IsSystem = true, SortOrder = 19 };
        var groceries = new Category { Name = "Groceries", SortOrder = 1 };
        dbContext.Categories.AddRange(income, groceries);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.DeleteAsync(income.Id, new DeleteCategoryRequest(groceries.Id), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    private static CategoriesController CreateController(ExpenseTracker.Infrastructure.AppDbContext dbContext)
        => new(dbContext, NullLogger<CategoriesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) },
                        authenticationType: "Test"))
                }
            }
        };
}
