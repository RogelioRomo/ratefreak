using RateFreak.Infrastructure.Updates;
namespace RateFreak.UnitTests;
public sealed class SemanticVersionTests
{
    [Theory]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("1.0.0-rc.10", "1.0.0-rc.2", 1)]
    [InlineData("1.0.0", "1.0.0-rc.10", 1)]
    [InlineData("1.0.0+a", "1.0.0+b", 0)]
    [InlineData("1.0.0-alpha", "1.0.0-1", 1)]
    public void ComparesSemanticPrecedence(string left, string right, int expected)
    {
        Assert.True(SemanticVersion.TryParse(left,out var a));
        Assert.True(SemanticVersion.TryParse(right,out var b));
        Assert.Equal(expected,Math.Sign(a!.CompareTo(b)));
    }
    [Theory]
    [InlineData("01.0.0")][InlineData("1.0.0-01")][InlineData("1.0")][InlineData("1.0.0\n")][InlineData("v1.0.0")]
    public void RejectsInvalidVersions(string value)=>Assert.False(SemanticVersion.TryParse(value,out _));
}
