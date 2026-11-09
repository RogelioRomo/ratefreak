using RateFreak.Domain.Catalog;

namespace RateFreak.UnitTests;

public sealed class CategoryRulesTests
{
    [Theory]
    [InlineData("  Blue   flowers  ", "Blue flowers", "BLUE FLOWERS")]
    [InlineData("Ｆｌｏｗｅｒｓ", "Flowers", "FLOWERS")]
    [InlineData("café", "café", "CAFÉ")]
    public void Names_normalize_for_duplicate_detection(string input, string display, string normalized)
    {
        var category = new Category(input, "public", Guid.NewGuid());
        Assert.Equal(display, category.Name);
        Assert.Equal(normalized, category.NormalizedName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\0name")]
    public void Invalid_names_are_rejected(string name) => Assert.Throws<ArgumentException>(() => new Category(name, "public", Guid.NewGuid()));

    [Fact]
    public void Private_categories_require_an_owner() => Assert.Throws<ArgumentException>(() => new Category("Flowers", "private", null));

    [Fact]
    public void Custom_items_are_private_owned_and_have_no_provider_metadata()
    {
        var owner = Guid.NewGuid();
        var category = Guid.NewGuid();
        var item = MediaItem.CreateCustom(owner, "Blue hydrangea", "A garden flower", category);
        Assert.Equal("private", item.Visibility);
        Assert.Equal("user", item.Provenance);
        Assert.Equal(owner, item.CreatorUserId);
        Assert.Equal(category, item.CategoryId);
        Assert.Equal("custom", item.MediaType);
        Assert.Equal("{}", item.Metadata);
        Assert.False(item.IsArchived);
    }

    [Fact]
    public void Legacy_constructor_cannot_create_an_unowned_custom_item() =>
        Assert.Throws<ArgumentException>(() => new MediaItem("custom", "Unowned"));
}
