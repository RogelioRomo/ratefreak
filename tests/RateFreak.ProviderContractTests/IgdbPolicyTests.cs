using Microsoft.Extensions.Options;
using RateFreak.Infrastructure.Providers.Igdb;

namespace RateFreak.ProviderContractTests;

public sealed class IgdbPolicyTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory();
    private string PolicyPath => Path.Combine(directory.FullName, "policy.json");
    private const string Empty = """{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":[]}""";
    private IgdbContentPolicy Policy => new(Options.Create(new IgdbOptions { PolicyPath = PolicyPath }));

    private void Write(string content)
    {
        File.WriteAllText(PolicyPath, content);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(PolicyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Theory]
    [InlineData("""{"format":1,"terminated":false,"terminated":true,"blockedIdentities":[],"blockedPosters":[]}""")]
    [InlineData("""{"format":1,"terminated":false,"blockedIdentities":["game:01"],"blockedPosters":[]}""")]
    [InlineData("""{"format":1,"terminated":false,"blockedIdentities":["game:2147483648"],"blockedPosters":[]}""")]
    [InlineData("""{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":["/test.jpg"]}""")]
    [InlineData("""{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":["x.jpg","x.jpg"]}""")]
    [InlineData("""{"format":1,"terminated":false,"blockedIdentities":[],"blockedPosters":[],"extra":0}""")]
    public void Invalid_policy_fails_closed(string content)
    {
        Write(content);
        Assert.Throws<IOException>(() => Policy.Read());
    }

    [Fact]
    public void Policy_requires_existing_private_regular_file_and_observes_replacement()
    {
        Assert.Throws<IOException>(() => Policy.Read());
        Write(Empty);
        Assert.False(Policy.Read().Terminated);
        Write(Empty.Replace("false", "true"));
        Assert.True(Policy.Read().Terminated);
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(PolicyPath, UnixFileMode.UserRead | UnixFileMode.GroupRead);
        Assert.Throws<IOException>(() => Policy.Read());
        File.Move(PolicyPath, PolicyPath + ".old");
        File.CreateSymbolicLink(PolicyPath, PolicyPath + ".old");
        Assert.Throws<IOException>(() => Policy.Read());
    }
    public void Dispose() => directory.Delete(true);
}
