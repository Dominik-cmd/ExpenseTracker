using ExpenseTracker.Application.Interfaces;
using ExpenseTracker.Application.Models;
using ExpenseTracker.Application.Services;
using ExpenseTracker.Core.Entities;
using FluentAssertions;
using Moq;

namespace ExpenseTracker.UnitTests;

public sealed class CategoryResolutionTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private readonly Mock<ICategoryRepository> _categoryRepo = new();
    private readonly Mock<ITransactionRepository> _transactionRepo = new();
    private readonly Mock<IMerchantRuleRepository> _merchantRuleRepo = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepo = new();

    private CategoryService CreateSubject()
      => new(_categoryRepo.Object, _transactionRepo.Object, _merchantRuleRepo.Object, _auditLogRepo.Object);

    [Fact]
    public async Task CreateAsync_ShouldAllowSecondLevelCategory()
    {
        var parent = new Category { Id = Guid.NewGuid(), Name = "Groceries", SortOrder = 1, UserId = TestUserId };

        _categoryRepo.Setup(x => x.GetByIdAsync(parent.Id, TestUserId, It.IsAny<CancellationToken>()))
          .ReturnsAsync(parent);
        _categoryRepo.Setup(x => x.GetNextSortOrderAsync(TestUserId, parent.Id, It.IsAny<CancellationToken>()))
          .ReturnsAsync(2);
        _categoryRepo.Setup(x => x.GetByIdWithSubCategoriesAsync(It.IsAny<Guid>(), TestUserId, It.IsAny<CancellationToken>()))
          .ReturnsAsync((Guid id, Guid _, CancellationToken _) => new Category
          {
              Id = id,
              Name = "Organic",
              ParentCategoryId = parent.Id,
              SortOrder = 2,
              UserId = TestUserId,
              SubCategories = new List<Category>()
          });

        var service = CreateSubject();

        var result = await service.CreateAsync(
          TestUserId,
          new CreateCategoryRequest("Organic", "#00ff00", "leaf", parent.Id),
          CancellationToken.None);

        result.Name.Should().Be("Organic");
        result.ParentCategoryId.Should().Be(parent.Id);
        result.IsSystem.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectThirdLevelCategory()
    {
        var parent = new Category { Id = Guid.NewGuid(), Name = "Groceries", SortOrder = 1, UserId = TestUserId };
        var child = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Mercator",
            ParentCategoryId = parent.Id,
            SortOrder = 1,
            UserId = TestUserId
        };

        _categoryRepo.Setup(x => x.GetByIdAsync(child.Id, TestUserId, It.IsAny<CancellationToken>()))
          .ReturnsAsync(child);

        var service = CreateSubject();

        var act = () => service.CreateAsync(
          TestUserId,
          new CreateCategoryRequest("Corner Shop", null, null, child.Id),
          CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
          .WithMessage("*two category levels*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowForSystemCategory()
    {
        var income = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Income",
            IsSystem = true,
            SortOrder = 19,
            UserId = TestUserId,
            SubCategories = new List<Category>()
        };

        _categoryRepo.Setup(x => x.GetByIdWithSubCategoriesAsync(income.Id, TestUserId, It.IsAny<CancellationToken>()))
          .ReturnsAsync(income);

        var service = CreateSubject();

        var act = () => service.UpdateAsync(
          income.Id,
          TestUserId,
          new UpdateCategoryRequest("Salary", null, null, null, null, null),
          CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
          .WithMessage("*System categories*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowForSystemCategory()
    {
        var income = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Income",
            IsSystem = true,
            SortOrder = 19,
            UserId = TestUserId,
            SubCategories = new List<Category>()
        };

        _categoryRepo.Setup(x => x.GetByIdWithSubCategoriesAsync(income.Id, TestUserId, It.IsAny<CancellationToken>()))
          .ReturnsAsync(income);

        var service = CreateSubject();

        var act = () => service.DeleteAsync(
          income.Id,
          TestUserId,
          new DeleteCategoryRequest(Guid.NewGuid()),
          CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
          .WithMessage("*System categories*");
    }
}
