namespace RateFreak.Domain.Catalog;

public sealed class CategoryAlias
{
    public Guid SourceCategoryId { get; private set; }
    public Guid TargetCategoryId { get; private set; }
    private CategoryAlias() { }
    public CategoryAlias(Guid sourceCategoryId, Guid targetCategoryId)
    {
        if (sourceCategoryId == targetCategoryId) throw new ArgumentException("A category cannot alias itself.");
        SourceCategoryId = sourceCategoryId; TargetCategoryId = targetCategoryId;
    }
}
