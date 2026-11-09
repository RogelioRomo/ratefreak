using RateFreak.Application.Collections;
namespace RateFreak.UnitTests;
public sealed class CollectionValidationTests
{
    [Theory]
    [InlineData("Garden", null, "private", true)]
    [InlineData("Garden", "Flowers", "public", true)]
    [InlineData("", null, "private", false)]
    [InlineData("  ", null, "private", false)]
    [InlineData("Bad\0name", null, "private", false)]
    [InlineData("Garden", null, "other", false)]
    public void Metadata_is_bounded_and_visibility_is_explicit(string name, string? description, string visibility, bool valid) => Assert.Equal(valid, CollectionValidation.Metadata(name, description, visibility).Count == 0);
    [Fact]
    public void Name_and_description_length_bounds_are_preserved()
    {
        Assert.Empty(CollectionValidation.Metadata(new string('x',120), new string('y',2000),"private"));
        Assert.NotEmpty(CollectionValidation.Metadata(new string('x',121), null,"private"));
        Assert.NotEmpty(CollectionValidation.Metadata("Garden", new string('y',2001),"private"));
    }
}
