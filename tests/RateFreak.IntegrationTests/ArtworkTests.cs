using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RateFreak.Application.Moderation;
using SkiaSharp;
using static RateFreak.IntegrationTests.CustomCatalogClient;

namespace RateFreak.IntegrationTests;

public sealed class ArtworkTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Artwork_is_on_disk_access_checked_and_stale_replacement_preserves_current_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "ratefreak-artwork-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services => services.Configure<RateFreak.Infrastructure.Artwork.ArtworkOptions>(o => { o.Enabled = true; o.RootPath = root; }));
            using var owner = Client(factory); using var anon = Client(factory); await SignIn(owner, Ct);
            var category = await Json(await owner.PostAsJsonAsync("/api/v1/categories", new { name = "Image flowers " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created);
            var item = await Json(await owner.PostAsJsonAsync("/api/v1/media", new { title = "Image flower", categoryId = category.GetProperty("id").GetGuid() }, Ct), Ct, HttpStatusCode.Created);
            var path = $"/api/v1/media/{item.GetProperty("id").GetGuid()}";
            using var bitmap = new SKBitmap(2, 2); bitmap.Erase(SKColors.Blue); using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            async Task<HttpResponseMessage> Upload(long revision, byte[] bytes)
            {
                using var form = new MultipartFormDataContent(); form.Add(new StringContent(revision.ToString(System.Globalization.CultureInfo.InvariantCulture)), "expectedRevision");
                form.Add(new ByteArrayContent(bytes), "file", "flower.png"); return await owner.PutAsync(path + "/artwork", form, Ct);
            }
            var uploaded = await Json(await Upload(1, data.ToArray()), Ct); var url = uploaded.GetProperty("imageReference").GetString()!;
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(url, Ct)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync(url, Ct)).StatusCode);
            Assert.Single(Directory.GetFiles(root, "*.webp"));
            Assert.Equal(HttpStatusCode.Conflict, (await Upload(1, data.ToArray())).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(2, [1, 2, 3])).StatusCode);
            await Json(await owner.PostAsJsonAsync(path + "/publish", new { expectedRevision = 2 }, Ct), Ct);
            var publicImage = await anon.GetAsync(url, Ct); Assert.Equal(HttpStatusCode.OK, publicImage.StatusCode); Assert.True(publicImage.Headers.CacheControl?.NoStore); Assert.Equal("image/webp", publicImage.Content.Headers.ContentType?.MediaType);
            using var staff = Client(factory); var staffId = await SignIn(staff, Ct);
            await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<IModerationService>().AppointAdminAsync(staffId, "Artwork inspection fixture", Ct);
            var report = await Json(await staff.PostAsJsonAsync(path + "/reports", new { reason = "spam" }, Ct), Ct, HttpStatusCode.Created);
            await Json(await owner.PostAsJsonAsync(path + "/unpublish", new { expectedRevision = 3 }, Ct), Ct);
            var assetId = url.Split('/').Last(); var reportArtwork = $"/api/v1/moderation/reports/{report.GetProperty("id").GetGuid()}/artwork/{assetId}";
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync(reportArtwork, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/moderation/media/{item.GetProperty("id").GetGuid()}/artwork/{assetId}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(reportArtwork, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync(url, Ct)).StatusCode);
            var replacement = await Json(await Upload(4, data.ToArray()), Ct); Assert.NotEqual(url, replacement.GetProperty("imageReference").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(url, Ct)).StatusCode);
            Assert.Single(Directory.GetFiles(root, "*.webp"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Metadata_failure_after_file_promotion_preserves_old_image_and_cleanup_keeps_referenced_bytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "ratefreak-rollback-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services => services.Configure<RateFreak.Infrastructure.Artwork.ArtworkOptions>(o => { o.Enabled = true; o.RootPath = root; }));
            using var owner = Client(factory); await SignIn(owner, Ct);
            var category = await Json(await owner.PostAsJsonAsync("/api/v1/categories", new { name = "Rollback image " + Guid.NewGuid(), scope = "public" }, Ct), Ct, HttpStatusCode.Created);
            var item = await Json(await owner.PostAsJsonAsync("/api/v1/media", new { title = "Rollback flower", categoryId = category.GetProperty("id").GetGuid() }, Ct), Ct, HttpStatusCode.Created); var id = item.GetProperty("id").GetGuid();
            using var bitmap = new SKBitmap(2,2); bitmap.Erase(SKColors.Red); using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png,100);
            async Task<HttpResponseMessage> Upload(long revision)
            { using var body = new MultipartFormDataContent(); body.Add(new StringContent(revision.ToString(System.Globalization.CultureInfo.InvariantCulture)),"expectedRevision"); body.Add(new ByteArrayContent(data.ToArray()),"file","flower.png"); return await owner.PutAsync($"/api/v1/media/{id}/artwork",body,Ct); }
            var uploaded = await Json(await Upload(1),Ct); var oldUrl = uploaded.GetProperty("imageReference").GetString()!;
            await using var db = new RateFreak.Infrastructure.Persistence.RateFreakDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<RateFreak.Infrastructure.Persistence.RateFreakDbContext>().UseNpgsql(database.ConnectionString).Options);
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fail_artwork_update() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Simulated artwork metadata failure'; END $$; CREATE TRIGGER fail_artwork_update BEFORE UPDATE ON artwork_assets FOR EACH ROW EXECUTE FUNCTION fail_artwork_update();",Ct);
            try { Assert.Equal(HttpStatusCode.InternalServerError,(await Upload(2)).StatusCode); }
            finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_artwork_update ON artwork_assets; DROP FUNCTION fail_artwork_update();",Ct); }
            Assert.Equal(oldUrl,(await Json(await owner.GetAsync($"/api/v1/media/{id}",Ct),Ct)).GetProperty("imageReference").GetString()); Assert.Equal(HttpStatusCode.OK,(await owner.GetAsync(oldUrl,Ct)).StatusCode);
            foreach(var path in Directory.GetFiles(root,"*.webp")) File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-2));
            using(var scope=factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<RateFreak.Infrastructure.Artwork.ArtworkCleanupService>().RunOnceAsync(Ct);
            Assert.Single(Directory.GetFiles(root,"*.webp")); Assert.Equal(HttpStatusCode.OK,(await owner.GetAsync(oldUrl,Ct)).StatusCode);
        }
        finally { if(Directory.Exists(root))Directory.Delete(root,true); }
    }

}
