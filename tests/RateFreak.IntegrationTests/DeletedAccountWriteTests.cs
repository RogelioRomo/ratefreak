using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.Collections;
using RateFreak.Application.PersonalMedia;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

[Trait("Category", "Database")]
public sealed class DeletedAccountWriteTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    // An authenticated request can reach a service after its actor was erased.
    // These tests exercise that service boundary, after middleware authentication.
    [Theory]
    [InlineData("library")]
    [InlineData("rating")]
    [InlineData("review")]
    [InlineData("collection")]
    public async Task Stale_actor_cannot_create_personal_data_or_trigger_a_foreign_key_error(string operation)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        var item = new MediaItem(MediaTypes.Movie, "Deleted actor regression " + Guid.NewGuid());
        db.Add(item);
        await db.SaveChangesAsync(ct);
        var erasedAccount = Guid.NewGuid();

        switch (operation)
        {
            case "library":
                Assert.Null(await scope.ServiceProvider.GetRequiredService<ILibraryService>()
                    .PutAsync(erasedAccount, item.Id, new("planned", null, null), ct));
                break;
            case "rating":
                Assert.Null(await scope.ServiceProvider.GetRequiredService<IRatingService>()
                    .PutAsync(erasedAccount, item.Id, 4m, ct));
                break;
            case "review":
                Assert.Equal(ReviewWriteStatus.NotFound, (await scope.ServiceProvider.GetRequiredService<IReviewService>()
                    .CreateAsync(erasedAccount, item.Id, new("This must not be saved.", false), ct)).Status);
                break;
            case "collection":
                Assert.Equal(CollectionStatus.NotFound, (await scope.ServiceProvider.GetRequiredService<ICollectionCommands>()
                    .CreateAsync(erasedAccount, new("This must not be saved.", null, "private"), ct)).Status);
                break;
        }
        Assert.False(await db.LibraryEntries.AnyAsync(x => x.UserId == erasedAccount, ct));
        Assert.False(await db.Ratings.AnyAsync(x => x.UserId == erasedAccount, ct));
        Assert.False(await db.Reviews.AnyAsync(x => x.UserId == erasedAccount, ct));
        Assert.False(await db.Collections.AnyAsync(x => x.OwnerUserId == erasedAccount, ct));
    }
}
