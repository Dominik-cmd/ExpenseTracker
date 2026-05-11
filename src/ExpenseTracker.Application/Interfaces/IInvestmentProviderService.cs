using ExpenseTracker.Application.Models;

namespace ExpenseTracker.Application.Interfaces;

public interface IInvestmentProviderService
{
    Task<object> GetProvidersAsync(Guid userId, CancellationToken ct);
    Task<bool> UpdateProviderAsync(Guid userId, Guid id, UpdateInvestmentProviderRequest request, CancellationToken ct);
    Task<object?> TestProviderAsync(Guid userId, Guid id, CancellationToken ct);
    Task<bool> EnableProviderAsync(Guid userId, Guid id, CancellationToken ct);
    Task<bool> DisableProviderAsync(Guid userId, Guid id, CancellationToken ct);
}
