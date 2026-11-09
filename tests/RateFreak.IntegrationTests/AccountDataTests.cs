using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RateFreak.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Authentication;
using RateFreak.Domain.Catalog;
using RateFreak.Domain.PersonalMedia;
using RateFreak.Domain.Moderation;
namespace RateFreak.IntegrationTests;

[Trait("Category", "Database")]
public sealed class AccountDataTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private const string Password = "Lifecycle-local-password-72";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static async Task Token(HttpClient client)
    {
        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body.GetProperty("requestToken").GetString());
    }
    private async Task<(ApiFactory Factory, HttpClient Client, Guid Id, string Handle)> Setup()
    {
        var factory = new ApiFactory(connectionString: database.ConnectionString);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        await Token(client); var handle = "data_" + Guid.NewGuid().ToString("N")[..12];
        using var registered = await client.PostAsJsonAsync("/api/v1/auth/register", new { handle, email = handle + "@example.test", password = Password, displayName = "Data owner" }, Ct);
        var account = await registered.Content.ReadFromJsonAsync<JsonElement>(Ct);
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = handle + "@example.test", password = Password }, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode); await Token(client);
        return (factory, client, account.GetProperty("id").GetGuid(), handle);
    }
    [Fact]
    public async Task Export_requires_confirmation_and_returns_secret_free_single_use_archive()
    {
        var (factory, client, id, handle) = await Setup(); await using var ownedFactory = factory; using var ownedClient = client;
        using var forbidden = await client.PostAsJsonAsync("/api/v1/users/me/data/export", new { }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, forbidden.StatusCode);
        using var confirmation = await client.PostAsJsonAsync("/api/v1/users/me/data/confirm", new { operation = "export", password = Password }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);
        using var export = await client.PostAsJsonAsync("/api/v1/users/me/data/export", new { }, Ct);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode); Assert.True(export.Headers.CacheControl?.NoStore);
        using var zip = new ZipArchive(await export.Content.ReadAsStreamAsync(Ct));
        Assert.NotNull(zip.GetEntry("manifest.json"));
        using var reader = new StreamReader(zip.GetEntry("account.json")!.Open()); var json = await reader.ReadToEndAsync(Ct);
        Assert.Contains(handle, json); Assert.DoesNotContain("passwordHash", json); Assert.DoesNotContain("securityStamp", json);
        using var replay = await client.PostAsJsonAsync("/api/v1/users/me/data/export", new { }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }
    [Fact]
    public async Task Delete_erases_private_dependencies_detaches_public_content_and_preserves_reportability()
    {
        var (factory, client, id, handle) = await Setup(); await using var ownedFactory = factory; using var ownedClient = client;
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var other = new AccountUser { Id = Guid.NewGuid(), UserName = "observer_" + Guid.NewGuid().ToString("N")[..10], Email = Guid.NewGuid() + "@example.test", DisplayName = "Observer" }; Assert.True((await users.CreateAsync(other, Password)).Succeeded);
        var publicCategory = new Category("Public " + Guid.NewGuid(), "public", id); var privateCategory = new Category("Private " + Guid.NewGuid(), "private", id); db.AddRange(publicCategory, privateCategory); await db.SaveChangesAsync(Ct);
        var retained = MediaItem.CreateCustom(id, "Retained public", "Keep", publicCategory.Id); var removed = MediaItem.CreateCustom(id, "Private item", "Remove", privateCategory.Id); db.AddRange(retained, removed); await db.SaveChangesAsync(Ct);
        await db.MediaItems.Where(m => m.Id == retained.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Visibility, "public").SetProperty(m => m.IsHidden, true), Ct);
        db.AddRange(new LibraryEntry(other.Id, retained.Id, "planned", null, null), new LibraryEntry(other.Id, removed.Id, "planned", null, null), new Review(other.Id, removed.Id, "Dependent review", false));
        var report = new ContentReport { TargetType = "item", TargetId = retained.Id, ItemId = retained.Id, OriginalAuthorId = id, OriginalReporterId = other.Id, ReporterId = other.Id, Reason = "spam" }; db.Add(report);
        var submitted = new ContentReport { TargetType = "item", TargetId = retained.Id, ItemId = retained.Id, OriginalAuthorId = id, OriginalReporterId = id, ReporterId = id, Reason = "other", Details = "Personal report text" }; db.Add(submitted);
        db.Add(new ModerationEvent { ActorId = id, Action = "appoint", TargetType = "account", TargetId = id, Reason = "Operator reason" }); await db.SaveChangesAsync(Ct);
        var path = Path.Combine(Path.GetTempPath(), "test-erasure-" + Guid.NewGuid() + ".jsonl");
        using (File.Create(path)) { }
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); scope.ServiceProvider.GetRequiredService<IOptions<AccountDataOptions>>().Value.ErasureJournalPath = path;
        try
        {
            var service = scope.ServiceProvider.GetRequiredService<IAccountDataService>(); var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct);
            var grant = await service.ConfirmAsync(id, user.SecurityStamp!, new("delete", Password), Ct); Assert.NotNull(grant);
            await service.DeleteAsync(id, user.SecurityStamp!, grant, handle, Ct); db.ChangeTracker.Clear();
            Assert.False(await db.Users.AnyAsync(u => u.Id == id, Ct)); Assert.False(await db.MediaItems.AnyAsync(m => m.Id == removed.Id, Ct));
            var item = await db.MediaItems.SingleAsync(m => m.Id == retained.Id, Ct); Assert.Null(item.CreatorUserId); Assert.True(item.IsHidden); Assert.Equal("user", item.Provenance); Assert.Equal(2, item.Revision);
            Assert.True(await db.LibraryEntries.AnyAsync(e => e.UserId == other.Id && e.MediaItemId == retained.Id, Ct)); Assert.False(await db.Reviews.AnyAsync(r => r.MediaItemId == removed.Id, Ct));
            var pseudonymized = await db.ContentReports.SingleAsync(r => r.Id == submitted.Id, Ct); Assert.NotEqual(id, pseudonymized.OriginalReporterId); Assert.Null(pseudonymized.ReporterId); Assert.Equal("Reporter account deleted.", pseudonymized.Details);
            Assert.False(await db.ModerationEvents.AnyAsync(e => e.ActorId == id || e.TargetType == "account" && e.TargetId == id, Ct));
            var moderation = scope.ServiceProvider.GetRequiredService<RateFreak.Application.Moderation.IModerationService>(); var target = await moderation.TargetAsync("item", retained.Id, Ct); Assert.NotNull(target); Assert.Null(target.Author);
            await db.MediaItems.Where(m => m.Id == retained.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.IsHidden, false), Ct);
            var reporter = new AccountUser { Id = Guid.NewGuid(), UserName = "reporter_" + Guid.NewGuid().ToString("N")[..10], Email = Guid.NewGuid() + "@example.test", DisplayName = "Reporter" }; Assert.True((await users.CreateAsync(reporter, Password)).Succeeded);
            var reported = await moderation.ReportAsync(reporter.Id, "item", retained.Id, new("spam", null), Ct); Assert.Equal(RateFreak.Application.Moderation.ModerationStatus.Success, reported.Status);
            Assert.Contains(id.ToString(), await File.ReadAllTextAsync(path, Ct));
            await scope.ServiceProvider.GetRequiredService<IAccountErasureMaintenance>().ReplayAsync(Ct);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Grants_are_operation_stamp_and_expiry_bound_and_budget_is_durable()
    {
        var (factory, client, id, _) = await Setup(); await using var ownedFactory = factory; using var ownedClient = client;
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>(); var service = scope.ServiceProvider.GetRequiredService<IAccountDataService>();
        var user = await db.Users.SingleAsync(u => u.Id == id, Ct); var stamp = user.SecurityStamp!;
        var wrong = await service.ConfirmAsync(id, stamp, new("delete", Password), Ct); Assert.NotNull(wrong);
        Assert.Equal(400, (await Assert.ThrowsAsync<AccountDataException>(() => service.ExportAsync(id, stamp, wrong, Ct))).StatusCode);
        var expired = await service.ConfirmAsync(id, stamp, new("export", Password), Ct);
        await db.Database.ExecuteSqlRawAsync("UPDATE account_data_grants SET expires_at = CURRENT_TIMESTAMP - INTERVAL '1 second' WHERE user_id = {0}", new object[] { id }, Ct);
        Assert.Equal(400, (await Assert.ThrowsAsync<AccountDataException>(() => service.ExportAsync(id, stamp, expired, Ct))).StatusCode);
        var revoked = await service.ConfirmAsync(id, stamp, new("export", Password), Ct);
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, Guid.NewGuid().ToString()), Ct);
        Assert.Equal(401, (await Assert.ThrowsAsync<AccountDataException>(() => service.ExportAsync(id, stamp, revoked, Ct))).StatusCode);
        db.ChangeTracker.Clear(); stamp = (await db.Users.SingleAsync(u => u.Id == id, Ct)).SecurityStamp!;
        for (var attempt = 0; attempt < 2; attempt++)
        { var grant = await service.ConfirmAsync(id, stamp, new("export", Password), Ct); await using var archive = await service.ExportAsync(id, stamp, grant, Ct); Assert.True(archive.CanSeek); }
        var limited = await service.ConfirmAsync(id, stamp, new("export", Password), Ct); Assert.Equal(429, (await Assert.ThrowsAsync<AccountDataException>(() => service.ExportAsync(id, stamp, limited, Ct))).StatusCode);
    }
    [Fact]
    public async Task Missing_journal_and_last_administrator_prevent_deletion_without_changing_account()
    {
        var (factory, client, id, handle) = await Setup(); await using var ownedFactory = factory; using var ownedClient = client;
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>(); var service = scope.ServiceProvider.GetRequiredService<IAccountDataService>();
        var user = await db.Users.SingleAsync(u => u.Id == id, Ct); var grant = await service.ConfirmAsync(id, user.SecurityStamp!, new("delete", Password), Ct);
        Assert.Equal(503, (await Assert.ThrowsAsync<AccountDataException>(() => service.DeleteAsync(id, user.SecurityStamp!, grant, handle, Ct))).StatusCode);
        Assert.True(await db.Users.AnyAsync(u => u.Id == id, Ct));
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(); if (!await roles.RoleExistsAsync("Admin")) Assert.True((await roles.CreateAsync(new("Admin"))).Succeeded);
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>().AddToRoleAsync(user, "Admin")).Succeeded);
        Assert.Equal(409, (await Assert.ThrowsAsync<AccountDataException>(() => service.DeleteAsync(id, user.SecurityStamp!, grant, handle, Ct))).StatusCode);
        Assert.True(await db.Users.AnyAsync(u => u.Id == id, Ct));
    }
    [Fact]
    public async Task Retention_removes_only_expired_recovery_resolved_reports_and_standalone_events_at_boundary()
    {
        var (factory, client, id, _) = await Setup(); await using var ownedFactory = factory; using var ownedClient = client;
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        var now = DateTimeOffset.UtcNow; now = new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO account_recovery_events(id,user_id,action,created_at) VALUES ({0},{1},'boundary',{2}),({3},{1},'recent',{4})", new object[] { Guid.NewGuid(), id, now.AddDays(-90), Guid.NewGuid(), now.AddDays(-90).AddSeconds(1) }, Ct);
        var expired = new ContentReport { OriginalReporterId = Guid.NewGuid(), OriginalAuthorId = id, TargetId = Guid.NewGuid(), TargetType = "item", Reason = "spam", Status = "resolved", ResolvedAt = now.AddDays(-365), ResolvedBy = id, Decision = "dismiss", ResolutionReason = "Reason" };
        var recent = new ContentReport { OriginalReporterId = Guid.NewGuid(), OriginalAuthorId = id, TargetId = Guid.NewGuid(), TargetType = "item", Reason = "spam", Status = "resolved", ResolvedAt = now.AddDays(-365).AddSeconds(1), ResolvedBy = id, Decision = "dismiss", ResolutionReason = "Reason" };
        var open = new ContentReport { OriginalReporterId = Guid.NewGuid(), OriginalAuthorId = id, TargetId = Guid.NewGuid(), TargetType = "item", Reason = "spam", CreatedAt = now.AddDays(-400) }; db.AddRange(expired, recent, open);
        var eventId = Guid.NewGuid(); db.AddRange(new ModerationEvent { Id = eventId, ReportId = expired.Id, TargetType = "item", TargetId = expired.TargetId, Action = "dismiss", Reason = "Reason", CreatedAt = now }, new ModerationEvent { ReportId = open.Id, TargetType = "item", TargetId = open.TargetId, Action = "hide", Reason = "Open evidence", CreatedAt = now.AddDays(-400) }, new ModerationEvent { TargetType = "account", TargetId = id, Action = "appoint", Reason = "Old event", CreatedAt = now.AddDays(-365) }); await db.SaveChangesAsync(Ct);
        await scope.ServiceProvider.GetRequiredService<IAccountErasureMaintenance>().RetainAsync(now, Ct); db.ChangeTracker.Clear();
        Assert.False(await db.ContentReports.AnyAsync(r => r.Id == expired.Id, Ct)); Assert.True(await db.ContentReports.AnyAsync(r => r.Id == recent.Id || r.Id == open.Id, Ct));
        Assert.False(await db.ModerationEvents.AnyAsync(e => e.Id == eventId || e.ReportId == null && e.CreatedAt <= now.AddDays(-365), Ct)); Assert.True(await db.ModerationEvents.AnyAsync(e => e.ReportId == open.Id, Ct));
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM account_recovery_events WHERE user_id = {0} AND action IN ('boundary','recent')", id).SingleAsync(Ct));
    }

    [Fact]
    public async Task Deletion_http_contract_clears_session_and_rejects_another_sessions_cookie()
    {
        var (factory,client,id,handle)=await Setup();await using var ownedFactory=factory;using var ownedClient=client;
        using var second=factory.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new("https://localhost"),AllowAutoRedirect=false});await Token(second);
        using var login=await second.PostAsJsonAsync("/api/v1/auth/login",new {email=handle+"@example.test",password=Password},Ct);Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        var path=Path.Combine(Path.GetTempPath(),"http-erasure-"+Guid.NewGuid()+".jsonl");
        using (File.Create(path)) { }
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        await using(var scope=factory.Services.CreateAsyncScope())scope.ServiceProvider.GetRequiredService<IOptions<AccountDataOptions>>().Value.ErasureJournalPath=path;
        try
        {
            using var confirm=await client.PostAsJsonAsync("/api/v1/users/me/data/confirm",new{operation="delete",password=Password},Ct);Assert.Equal(HttpStatusCode.NoContent,confirm.StatusCode);
            using var wrong=await client.PostAsJsonAsync("/api/v1/users/me/data/delete",new{handle="wrong"},Ct);Assert.Equal(HttpStatusCode.BadRequest,wrong.StatusCode);
            using var deletion=await client.PostAsJsonAsync("/api/v1/users/me/data/delete",new{handle},Ct);Assert.Equal(HttpStatusCode.NoContent,deletion.StatusCode);
            Assert.Contains(deletion.Headers.GetValues("Set-Cookie"),c=>c.StartsWith("__Secure-ratefreak.lifecycle=",StringComparison.Ordinal)&&c.Contains("expires=",StringComparison.OrdinalIgnoreCase));
            foreach(var browser in new[]{client,second}){using var me=await browser.GetAsync("/api/v1/users/me",Ct);Assert.Equal(HttpStatusCode.Unauthorized,me.StatusCode);}
        }
        finally{File.Delete(path);}
    }
    [Fact]
    public async Task Failed_database_erasure_rolls_back_and_durable_pending_intent_replays()
    {
        var (factory,client,id,handle)=await Setup();await using var ownedFactory=factory;using var ownedClient=client;
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();var service=scope.ServiceProvider.GetRequiredService<IAccountDataService>();
        var user=await db.Users.SingleAsync(u=>u.Id==id,Ct);var category=new Category("Rollback "+Guid.NewGuid(),"public",id);db.Add(category);await db.SaveChangesAsync(Ct);
        var path=Path.Combine(Path.GetTempPath(),"pending-erasure-"+Guid.NewGuid()+".jsonl");
        using (File.Create(path)) { }
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);scope.ServiceProvider.GetRequiredService<IOptions<AccountDataOptions>>().Value.ErasureJournalPath=path;
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION reject_lifecycle_delete() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected erasure failure'; END $$; CREATE TRIGGER reject_lifecycle_delete BEFORE DELETE ON users FOR EACH ROW EXECUTE FUNCTION reject_lifecycle_delete();",Ct);
        try
        {
            var grant=await service.ConfirmAsync(id,user.SecurityStamp!,new("delete",Password),Ct);Assert.NotNull(grant);
            await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>service.DeleteAsync(id,user.SecurityStamp!,grant,handle,Ct));
            Assert.True(await db.Users.AnyAsync(u=>u.Id==id,Ct));Assert.True(await db.Categories.AnyAsync(c=>c.Id==category.Id&&c.OwnerUserId==id,Ct));
            var record=JsonDocument.Parse((await File.ReadAllLinesAsync(path,Ct)).Single());Assert.False(record.RootElement.GetProperty("Completed").GetBoolean());
        }
        finally{await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_lifecycle_delete ON users; DROP FUNCTION reject_lifecycle_delete();",Ct);}
        try
        {
            await scope.ServiceProvider.GetRequiredService<IAccountErasureMaintenance>().ReplayAsync(Ct);db.ChangeTracker.Clear();
            Assert.False(await db.Users.AnyAsync(u=>u.Id==id,Ct));Assert.Null((await db.Categories.SingleAsync(c=>c.Id==category.Id,Ct)).OwnerUserId);
            var lines=await File.ReadAllLinesAsync(path,Ct);Assert.True(JsonDocument.Parse(lines.Last()).RootElement.GetProperty("Completed").GetBoolean());
        }
        finally{File.Delete(path);}
    }

    [Fact]
    public async Task Journal_replays_durable_intents_ignores_and_repairs_only_torn_final_suffix_and_rejects_complete_corruption()
    {
        var (factory,client,id,_)=await Setup();await using var ownedFactory=factory;using var ownedClient=client;
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<RateFreakDbContext>();
        var other=new AccountUser{Id=Guid.NewGuid(),UserName="torn_"+Guid.NewGuid().ToString("N")[..12],Email=Guid.NewGuid()+"@example.test",DisplayName="Uncommitted intent"};Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>().CreateAsync(other,Password)).Succeeded);
        var path=Path.Combine(Path.GetTempPath(),"torn-erasure-"+Guid.NewGuid()+".jsonl");
        var durable=JsonSerializer.Serialize(new{AccountId=id,Timestamp=DateTimeOffset.UtcNow,Completed=false});
        await File.WriteAllTextAsync(path,durable+"\n"+"{\"AccountId\":\""+other.Id+"\",\"Timestamp\":",Ct);
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);
        scope.ServiceProvider.GetRequiredService<IOptions<AccountDataOptions>>().Value.ErasureJournalPath=path;
        try
        {
            var maintenance=scope.ServiceProvider.GetRequiredService<IAccountErasureMaintenance>();await maintenance.ReplayAsync(Ct);
            Assert.False(await db.Users.AnyAsync(u=>u.Id==id,Ct));Assert.True(await db.Users.AnyAsync(u=>u.Id==other.Id,Ct));
            var lines=await File.ReadAllLinesAsync(path,Ct);Assert.Equal(2,lines.Length);
            Assert.All(lines,line=>{using var record=JsonDocument.Parse(line);Assert.Equal(id,record.RootElement.GetProperty("AccountId").GetGuid());});
            await File.AppendAllTextAsync(path,"{\"AccountId\":\n",Ct);
            await Assert.ThrowsAsync<IOException>(()=>maintenance.ReplayAsync(Ct));
            Assert.True(await db.Users.AnyAsync(u=>u.Id==other.Id,Ct));
        }
        finally{File.Delete(path);}
    }

}
