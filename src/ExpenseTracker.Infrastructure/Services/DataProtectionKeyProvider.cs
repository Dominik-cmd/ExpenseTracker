using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Infrastructure;

public static class DataProtectionKeyProvider
{
    public static IServiceCollection AddExpenseTrackerDataProtection(this IServiceCollection services)
    {
        var keyPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEY_PATH");
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            throw new InvalidOperationException("DATA_PROTECTION_KEY_PATH environment variable must be configured.");
        }

        var directory = new DirectoryInfo(keyPath);
        if (!directory.Exists)
        {
            directory.Create();
        }

        services.AddDataProtection()
            .PersistKeysToFileSystem(directory);

        return services;
    }
}
