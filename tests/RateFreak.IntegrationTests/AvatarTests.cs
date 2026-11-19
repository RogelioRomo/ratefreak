using System.Net;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Infrastructure.Persistence;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.Moderation;
using RateFreak.Infrastructure.Artwork;
using SkiaSharp;
using static RateFreak.IntegrationTests.CustomCatalogClient;

namespace RateFreak.IntegrationTests;

public sealed class AvatarTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Upload_replace_remove_and_moderation_enforce_current_image_visibility()
    {
        var root = Path.Combine(Path.GetTempPath(), "rf-avatar-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services => services.Configure<ArtworkOptions>(o => { o.Enabled = true; o.RootPath = root; }));
            using var owner = Client(factory); using var anon = Client(factory); using var staff = Client(factory);
            var userId = await SignIn(owner, Ct); var staffId = await SignIn(staff, Ct);
            owner.DefaultRequestHeaders.Add("X-RateFreak-Account-Id", userId.ToString());
            using var bitmap = new SKBitmap(20, 40); bitmap.Erase(SKColors.Blue); using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            async Task<HttpResponseMessage> Upload(long revision, byte[]? bytes = null)
            {
                using var body = new MultipartFormDataContent(); body.Add(new StringContent(revision.ToString()), "expectedRevision");
                body.Add(new ByteArrayContent(bytes ?? png.ToArray()), "file", "avatar.png"); return await owner.PutAsync("/api/v1/users/me/avatar", body, Ct);
            }
            var uploaded = await Json(await Upload(0), Ct); var ownerUrl = uploaded.GetProperty("avatarUrl").GetString()!;
            var asset = ownerUrl.Split('/').Last(); var publicUrl = $"/api/v1/users/{userId}/avatar/{asset}";
            Assert.Single(Directory.GetFiles(Path.Combine(root, "avatars"), "*.webp")); Assert.Empty(Directory.GetFiles(root, "*.webp"));
            var read = await anon.GetAsync(publicUrl, Ct); Assert.Equal(HttpStatusCode.OK, read.StatusCode); Assert.True(read.Headers.CacheControl?.NoStore);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(ownerUrl, Ct)).StatusCode);
            owner.DefaultRequestHeaders.Remove("X-RateFreak-Account-Id"); owner.DefaultRequestHeaders.Add("X-RateFreak-Account-Id", staffId.ToString());
            Assert.Equal(HttpStatusCode.Conflict, (await Upload(1)).StatusCode);
            using (var wrongRemove = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/me/avatar") { Content = JsonContent.Create(new { expectedRevision = 1 }) })
                Assert.Equal(HttpStatusCode.Conflict, (await owner.SendAsync(wrongRemove, Ct)).StatusCode);
            owner.DefaultRequestHeaders.Remove("X-RateFreak-Account-Id"); owner.DefaultRequestHeaders.Add("X-RateFreak-Account-Id", userId.ToString());
            var profile = await Json(await anon.GetAsync($"/api/v1/users/{userId}", Ct), Ct);
            Assert.Equal(publicUrl, profile.GetProperty("avatarUrl").GetString());
            var account = await Json(await owner.GetAsync("/api/v1/users/me", Ct), Ct);
            Assert.Equal(1, account.GetProperty("avatarRevision").GetInt64());
            using var unsigned = new HttpClient();
            var csrf = owner.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();
            owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(1)).StatusCode);
            owner.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf);
            Assert.Equal(HttpStatusCode.Conflict, (await Upload(0)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(1, [1, 2, 3])).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync(publicUrl, Ct)).StatusCode);
            await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<IModerationService>().AppointAdminAsync(staffId, "Avatar fixture", Ct);
            var report = await Json(await staff.PostAsJsonAsync($"/api/v1/users/{userId}/reports", new { reason = "spam" }, Ct), Ct, HttpStatusCode.Created);
            var reportId = report.GetProperty("id").GetGuid();
            var inspection = await Json(await staff.GetAsync($"/api/v1/moderation/reports/{reportId}", Ct), Ct);
            var version = inspection.GetProperty("target").GetProperty("updatedAt").GetString();
            Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync($"/api/v1/moderation/reports/{reportId}/resolve", new { decision = "hide", reason = "Hide avatar", expectedTargetUpdatedAt = version }, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync(publicUrl, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(ownerUrl, Ct)).StatusCode);
            Assert.Equal(System.Text.Json.JsonValueKind.Null, (await Json(await anon.GetAsync($"/api/v1/users/{userId}", Ct), Ct)).GetProperty("avatarUrl").ValueKind);
            Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync("/api/v1/users/me/data/confirm", new { operation = "export", password = "Local-password-72" }, Ct)).StatusCode);
            var exported = await owner.PostAsJsonAsync("/api/v1/users/me/data/export", new {}, Ct);
            Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
            using (var zip = new ZipArchive(await exported.Content.ReadAsStreamAsync(Ct)))
            {
                Assert.NotNull(zip.GetEntry("avatar.json"));
                Assert.NotNull(zip.GetEntry($"avatar/{asset}.webp"));
            }
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/v1/moderation/reports/{reportId}/avatar/{asset}", Ct)).StatusCode);
            var direct = await Json(await staff.GetAsync($"/api/v1/moderation/profiles/{userId}", Ct), Ct);
            var directImage = direct.GetProperty("imageReference").GetString(); Assert.NotNull(directImage);
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync(directImage, Ct)).StatusCode);
            inspection = await Json(await staff.GetAsync($"/api/v1/moderation/reports/{reportId}", Ct), Ct);
            version = inspection.GetProperty("target").GetProperty("updatedAt").GetString();
            var replacement = await Json(await Upload(1), Ct); var newUrl = replacement.GetProperty("avatarUrl").GetString()!;
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(ownerUrl, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/users/{userId}/avatar/{newUrl.Split('/').Last()}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync($"/api/v1/moderation/profiles/{userId}/visibility", new { hidden = false, reason = "Stale restore", expectedTargetUpdatedAt = version }, Ct)).StatusCode);
            inspection = await Json(await staff.GetAsync($"/api/v1/moderation/reports/{reportId}", Ct), Ct);
            Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync($"/api/v1/moderation/profiles/{userId}/visibility", new { hidden = false, reason = "Restore current avatar", expectedTargetUpdatedAt = inspection.GetProperty("target").GetProperty("updatedAt").GetString() }, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/v1/users/{userId}/avatar/{newUrl.Split('/').Last()}", Ct)).StatusCode);
            using var remove = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/me/avatar") { Content = JsonContent.Create(new { expectedRevision = 2 }) };
            await Json(await owner.SendAsync(remove, Ct), Ct);
            Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(newUrl, Ct)).StatusCode);
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "avatars"), "*.webp"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task Concurrent_uploads_cleanup_and_erasure_replay_preserve_only_current_owned_images()
    {
        var root = Path.Combine(Path.GetTempPath(), "rf-avatar-life-" + Guid.NewGuid().ToString("N"));
        var journal = root + ".jsonl";
        try
        {
            await File.WriteAllTextAsync(journal, "", Ct);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(journal, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await using var factory = new ApiFactory(connectionString: database.ConnectionString, configureServices: services => {
                services.Configure<ArtworkOptions>(o => { o.Enabled = true; o.RootPath = root; });
                services.Configure<AccountDataOptions>(o => o.ErasureJournalPath = journal);
            });
            using var owner = Client(factory); using var anon = Client(factory); var id = await SignIn(owner, Ct);
            owner.DefaultRequestHeaders.Add("X-RateFreak-Account-Id", id.ToString());
            using var bitmap = new SKBitmap(10, 10); bitmap.Erase(SKColors.Green); using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            async Task<HttpResponseMessage> Upload()
            {
                using var body = new MultipartFormDataContent(); body.Add(new StringContent("0"), "expectedRevision"); body.Add(new ByteArrayContent(png.ToArray()), "file", "image.png");
                return await owner.PutAsync("/api/v1/users/me/avatar", body, Ct);
            }
            var results = await Task.WhenAll(Upload(), Upload());
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
            var current = await Json(results.Single(r => r.StatusCode == HttpStatusCode.OK), Ct); var assetId = Guid.Parse(current.GetProperty("avatarUrl").GetString()!.Split('/').Last());
            var folder = Path.Combine(root, "avatars"); var orphan = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".webp"); await File.WriteAllBytesAsync(orphan, [1], Ct);
            foreach (var file in Directory.GetFiles(folder, "*.webp")) File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-2));
            await using var scope = factory.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
            await services.GetRequiredService<AvatarCleanupService>().RunOnceAsync(Ct);
            Assert.False(File.Exists(orphan)); Assert.Single(Directory.GetFiles(folder, "*.webp"));
            var guarded=Path.Combine(folder,Guid.NewGuid().ToString("N")+".webp");await File.WriteAllBytesAsync(guarded,[2],Ct);File.SetLastWriteTimeUtc(guarded,DateTime.UtcNow.AddDays(-2));
            var lease=Path.ChangeExtension(guarded,".lease");var victim=Path.Combine(root,"lease-victim");await File.WriteAllTextAsync(victim,"keep",Ct);File.CreateSymbolicLink(lease,victim);
            await services.GetRequiredService<AvatarCleanupService>().RunOnceAsync(Ct);Assert.True(File.Exists(guarded));Assert.Equal("keep",await File.ReadAllTextAsync(victim,Ct));
            File.Delete(lease);await services.GetRequiredService<AvatarCleanupService>().RunOnceAsync(Ct);Assert.False(File.Exists(guarded));
            var db = services.GetRequiredService<RateFreakDbContext>(); var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct);
            var saved = await db.Set<AvatarAsset>().AsNoTracking().SingleAsync(a => a.UserId == id, Ct);
            var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, saved.StorageKey), Ct);
            var lifecycle = services.GetRequiredService<IAccountDataService>();
            var grant = await lifecycle.ConfirmAsync(id, user.SecurityStamp!, new("delete", "Local-password-72"), Ct);
            await lifecycle.DeleteAsync(id, user.SecurityStamp!, grant, user.UserName!, Ct);
            Assert.Empty(Directory.GetFiles(folder, "*.webp")); Assert.False(await db.Set<AvatarAsset>().AnyAsync(a => a.UserId == id, Ct));
            Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/users/{id}/avatar/{assetId}", Ct)).StatusCode);
            // Simulate restoration of the deleted account/image, then replay the durable journal.
            user.ConcurrencyStamp = Guid.NewGuid().ToString(); db.ChangeTracker.Clear(); db.Users.Add(user); db.Add(saved); await db.SaveChangesAsync(Ct);
            await File.WriteAllBytesAsync(Path.Combine(folder, saved.StorageKey), bytes, Ct);
            await services.GetRequiredService<IAccountErasureMaintenance>().ReplayAsync(Ct);
            Assert.False(await db.Users.AnyAsync(u => u.Id == id, Ct)); Assert.False(await db.Set<AvatarAsset>().AnyAsync(a => a.UserId == id, Ct)); Assert.Empty(Directory.GetFiles(folder, "*.webp"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); if (File.Exists(journal)) File.Delete(journal); }
    }

    [Fact]
    public async Task Direct_hide_without_a_report_keeps_current_avatar_inspectable_for_restore()
    {
        var root=Path.Combine(Path.GetTempPath(),"rf-avatar-direct-"+Guid.NewGuid().ToString("N"));
        try
        {
            await using var factory=new ApiFactory(connectionString:database.ConnectionString,configureServices:services=>services.Configure<ArtworkOptions>(o=>{o.Enabled=true;o.RootPath=root;}));
            using var owner=Client(factory);using var staff=Client(factory);var id=await SignIn(owner,Ct);var actor=await SignIn(staff,Ct);
            owner.DefaultRequestHeaders.Add("X-RateFreak-Account-Id",id.ToString());
            using var bitmap=new SKBitmap(2,2);bitmap.Erase(SKColors.Red);using var image=SKImage.FromBitmap(bitmap);using var png=image.Encode(SKEncodedImageFormat.Png,100);
            using var body=new MultipartFormDataContent();body.Add(new StringContent("0"),"expectedRevision");body.Add(new ByteArrayContent(png.ToArray()),"file","avatar.png");await Json(await owner.PutAsync("/api/v1/users/me/avatar",body,Ct),Ct);
            await using(var scope=factory.Services.CreateAsyncScope())await scope.ServiceProvider.GetRequiredService<IModerationService>().AppointAdminAsync(actor,"Direct avatar fixture",Ct);
            var target=await Json(await staff.GetAsync($"/api/v1/moderation/profiles/{id}",Ct),Ct);
            Assert.NotNull(target.GetProperty("imageReference").GetString());
            Assert.Equal(HttpStatusCode.NoContent,(await staff.PostAsJsonAsync($"/api/v1/moderation/profiles/{id}/visibility",new{hidden=true,reason="Direct hide",expectedTargetUpdatedAt=target.GetProperty("updatedAt").GetString()},Ct)).StatusCode);
            target=await Json(await staff.GetAsync($"/api/v1/moderation/profiles/{id}",Ct),Ct);var url=target.GetProperty("imageReference").GetString();Assert.NotNull(url);
            Assert.Equal(HttpStatusCode.OK,(await staff.GetAsync(url,Ct)).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await owner.GetAsync(url,Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent,(await staff.PostAsJsonAsync($"/api/v1/moderation/profiles/{id}/visibility",new{hidden=false,reason="Inspected current avatar",expectedTargetUpdatedAt=target.GetProperty("updatedAt").GetString()},Ct)).StatusCode);
            // Once retained inspection scope expires, a hidden avatar is neither readable nor restorable without removal.
            target=await Json(await staff.GetAsync($"/api/v1/moderation/profiles/{id}",Ct),Ct);
            Assert.Equal(HttpStatusCode.NoContent,(await staff.PostAsJsonAsync($"/api/v1/moderation/profiles/{id}/visibility",new{hidden=true,reason="Scope retention fixture",expectedTargetUpdatedAt=target.GetProperty("updatedAt").GetString()},Ct)).StatusCode);
            await using(var scope=factory.Services.CreateAsyncScope())await scope.ServiceProvider.GetRequiredService<RateFreakDbContext>().ModerationEvents.Where(e=>e.TargetType=="profile"&&e.TargetId==id).ExecuteDeleteAsync(Ct);
            Assert.Equal(HttpStatusCode.NotFound,(await staff.GetAsync($"/api/v1/moderation/profiles/{id}/avatar/"+target.GetProperty("imageReference").GetString()!.Split('/').Last(),Ct)).StatusCode);
            target=await Json(await staff.GetAsync($"/api/v1/moderation/profiles/{id}",Ct),Ct);Assert.True(target.GetProperty("avatarUnavailable").GetBoolean());
            Assert.Equal(HttpStatusCode.Conflict,(await staff.PostAsJsonAsync($"/api/v1/moderation/profiles/{id}/visibility",new{hidden=false,reason="No retained scope",expectedTargetUpdatedAt=target.GetProperty("updatedAt").GetString()},Ct)).StatusCode);
            using var remove=new HttpRequestMessage(HttpMethod.Delete,"/api/v1/users/me/avatar"){Content=JsonContent.Create(new{expectedRevision=1})};await Json(await owner.SendAsync(remove,Ct),Ct);
            target=await Json(await staff.GetAsync($"/api/v1/moderation/profiles/{id}",Ct),Ct);Assert.False(target.GetProperty("avatarUnavailable").GetBoolean());
            Assert.Equal(HttpStatusCode.NoContent,(await staff.PostAsJsonAsync($"/api/v1/moderation/profiles/{id}/visibility",new{hidden=false,reason="Owner removed image",expectedTargetUpdatedAt=target.GetProperty("updatedAt").GetString()},Ct)).StatusCode);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }

}
