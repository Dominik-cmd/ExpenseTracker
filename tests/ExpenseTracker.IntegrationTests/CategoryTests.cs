using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.IntegrationTests;

public sealed class CategoryTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetCategories_ReturnsHierarchyWithSubcategories()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await ReadAsAsync<List<CategoryDto>>(response);
        Assert.NotNull(payload);
        Assert.Contains(payload!, category =>
            category.Name == "Groceries" &&
            category.SubCategories.Any(subCategory => subCategory.Name == "Mercator"));
    }

    [Fact]
    public async Task CreateCategory_CreatesNewCategory()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/categories",
            new CreateCategoryRequest("Pets", "#123456", "paw", null),
            CustomWebApplicationFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await ReadAsAsync<CategoryDto>(response);
        Assert.NotNull(payload);
        Assert.Equal("Pets", payload!.Name);
        Assert.False(payload.IsSystem);

        var exists = await Factory.ExecuteDbContextAsync(db =>
            db.Categories.AnyAsync(x => x.Name == "Pets" && x.ParentCategoryId == null));

        Assert.True(exists);
    }

    [Fact]
    public async Task DeleteSystemCategory_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync();
        var incomeId = await GetCategoryIdAsync("Income");
        var uncategorizedId = await GetCategoryIdAsync("Uncategorized");

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/categories/{incomeId}")
        {
            Content = JsonContent.Create(
                new DeleteCategoryRequest(uncategorizedId),
                options: CustomWebApplicationFactory.JsonOptions)
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
