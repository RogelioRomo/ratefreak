using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RateFreak.Infrastructure;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.UnitTests;

public sealed class DatabaseOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not a connection string")]
    [InlineData("Host=localhost;Database=test;Username=test;Port=hello")]
    [InlineData("Host=localhost;Username=test;Password=DO_NOT_LEAK")]
    public void Invalid_configuration_is_rejected_without_exposing_credentials(string? value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Default"] = value }).Build();
        using var services = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        var exception = Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<IOptions<DatabaseOptions>>().Value);
        Assert.DoesNotContain("DO_NOT_LEAK", exception.Message);
    }

    [Fact]
    public void Valid_configuration_can_be_resolved_without_connecting()
    {
        const string connection = "Host=localhost;Database=ratefreak;Username=test;Password=test";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connection }).Build();
        using var services = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        Assert.Equal(connection, services.GetRequiredService<IOptions<DatabaseOptions>>().Value.Default);
        using var scope = services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RateFreakDbContext>());
    }
}
