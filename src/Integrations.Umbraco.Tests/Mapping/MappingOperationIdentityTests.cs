using Microsoft.Extensions.DependencyInjection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco.Mapping;

namespace Relewise.Integrations.Umbraco.Tests.Mapping;

public class MappingOperationIdentityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdentityClaimsRemainExclusiveAcrossBatchesButAllowTheSameDocumentAgain(bool brand)
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var operation = new MappingOperation(services, CancellationToken.None);
        var key = Guid.NewGuid();
        Type entityType = brand ? typeof(Brand) : typeof(Product);
        operation.ClaimIdentity(entityType, "same", key);
        operation.ClaimIdentity(entityType, "same", key);
        string conflictingId = brand ? "SAME" : "same";
        Assert.Throws<InvalidOperationException>(() =>
            operation.ClaimIdentity(entityType, conflictingId, Guid.NewGuid()));
    }
}
