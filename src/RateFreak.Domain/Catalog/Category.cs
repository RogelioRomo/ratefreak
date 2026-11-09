using System.Text;

namespace RateFreak.Domain.Catalog;

public sealed class Category
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public string Scope { get; private set; } = null!;
    public Guid? OwnerUserId { get; private set; }
    public bool IsHidden { get; private set; }
    public bool IsSeeded { get; private set; }
    public long Revision { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    private Category() { }

    public Category(string name, string scope, Guid? ownerUserId)
    {
        if (scope is not ("public" or "private") || (scope == "private" && (ownerUserId is null || ownerUserId == Guid.Empty)))
            throw new ArgumentException("Invalid category scope or owner.");
        Name = DisplayName(name); NormalizedName = Name.ToUpperInvariant(); Scope = scope; OwnerUserId = ownerUserId;
    }

    public static string DisplayName(string name)
    {
        if (name.Any(c => char.IsControl(c) && !char.IsWhiteSpace(c))) throw new ArgumentException("Invalid category name.");
        name = string.Join(' ', name.Normalize(NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (name.Length is < 1 or > 100 || name.Any(char.IsControl)) throw new ArgumentException("Category names require 1–100 printable characters.");
        return name;
    }

    public static Guid ForMediaType(string type) => type switch
    {
        MediaTypes.Movie => Guid.Parse("f11e0000-0000-0000-0000-000000000001"),
        MediaTypes.TvShow => Guid.Parse("f11e0000-0000-0000-0000-000000000002"),
        MediaTypes.Album => Guid.Parse("f11e0000-0000-0000-0000-000000000003"),
        MediaTypes.VideoGame => Guid.Parse("f11e0000-0000-0000-0000-000000000004"),
        _ => throw new ArgumentException("No seeded category for this media type.")
    };
}
