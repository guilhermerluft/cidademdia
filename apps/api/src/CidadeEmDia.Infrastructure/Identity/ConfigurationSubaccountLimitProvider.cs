using CidadeEmDia.Application.Subaccounts;
using Microsoft.Extensions.Configuration;

namespace CidadeEmDia.Infrastructure.Identity;

internal sealed class ConfigurationSubaccountLimitProvider : ISubaccountLimitProvider
{
    private readonly int? configuredLimit;

    public ConfigurationSubaccountLimitProvider(IConfiguration configuration)
    {
        var raw = configuration["SUBACCOUNT_DEFAULT_LIMIT"];
        configuredLimit = int.TryParse(raw, out var value) && value >= 0
            ? value
            : null;
    }

    public Task<int?> GetLimitAsync(Guid masterUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(configuredLimit);
}
