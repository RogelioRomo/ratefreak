using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
namespace RateFreak.IntegrationTests;

public sealed partial class NotificationTests
{
    [Theory]
    [InlineData("{\"id\":\"mistaken-single-delete\"}")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    public async Task Unexpected_mutation_payload_cannot_clear_the_inbox(string payload) {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f);
        await SignIn(actor); var ownerId=await SignIn(owner);
        NoContent(await actor.PutAsync($"/api/v1/users/{ownerId}/follow",null,Ct));
        var response=await owner.SendAsync(new HttpRequestMessage(HttpMethod.Delete,"/api/v1/notifications") { Content=new StringContent(payload,System.Text.Encoding.UTF8,"application/json") },Ct);
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode); Assert.Equal(1,await Count(owner));
    }
    [Fact]
    public async Task Failed_notification_insert_rolls_back_follow_and_comment() {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f);
        var actorId=await SignIn(actor); var ownerId=await SignIn(owner); var (_,review)=await Review(owner);
        await using var db=Context();
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION reject_notification_insert() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected notification failure'; END $$; CREATE TRIGGER reject_notification_insert BEFORE INSERT ON notifications FOR EACH ROW EXECUTE FUNCTION reject_notification_insert();",Ct);
        try {
            Assert.Equal(HttpStatusCode.InternalServerError,(await actor.PutAsync($"/api/v1/users/{ownerId}/follow",null,Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError,(await actor.PostAsJsonAsync($"/api/v1/reviews/{review}/comments",new {body="Rollback"},Ct)).StatusCode);
            Assert.False(await db.UserFollows.AnyAsync(x=>x.FollowerId==actorId&&x.FolloweeId==ownerId,Ct));
            Assert.False(await db.ReviewComments.AnyAsync(x=>x.ReviewId==review,Ct)); Assert.False(await db.Notifications.AnyAsync(x=>x.RecipientId==ownerId,Ct));
        } finally {await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_notification_insert ON notifications; DROP FUNCTION reject_notification_insert();",Ct);}
    }
    [Fact]
    public async Task Failed_clear_rolls_back_every_deletion_and_read_state() {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f);
        await SignIn(actor); var ownerId=await SignIn(owner); var (_,review)=await Review(owner); await Comment(actor,review); await Comment(actor,review);
        var before=await Inbox(owner); var id=before.GetProperty("items")[0].GetProperty("id").GetGuid(); NoContent(await owner.PutAsync($"/api/v1/notifications/{id}/read",null,Ct));
        await using var db=Context();
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION reject_notification_clear() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected clear failure'; END $$; CREATE TRIGGER reject_notification_clear AFTER DELETE ON notifications FOR EACH STATEMENT EXECUTE FUNCTION reject_notification_clear();",Ct);
        try {
            Assert.Equal(HttpStatusCode.InternalServerError,(await owner.DeleteAsync("/api/v1/notifications",Ct)).StatusCode);
            Assert.Equal(2,await db.Notifications.CountAsync(x=>x.RecipientId==ownerId,Ct)); Assert.Equal(1,await Count(owner));
        } finally {await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_notification_clear ON notifications; DROP FUNCTION reject_notification_clear();",Ct);}
    }
    [Fact]
    public async Task Inbox_projection_executes_on_PostgreSQL() {
        await using var f=Factory(); await using var scope=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateAsyncScope(f.Services);
        var service=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<RateFreak.Application.Social.INotificationService>(scope.ServiceProvider);
        var page=await service.ListAsync(Guid.NewGuid(),1,20,Ct); Assert.Empty(page.Items);
    }
    [Fact]
    public async Task Pagination_and_clear_cover_every_page_without_touching_other_recipients() {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f); using var other=Client(f);
        await SignIn(actor); await SignIn(owner); await SignIn(other); var (_,review)=await Review(owner); var (_,otherReview)=await Review(other);
        for(var i=0;i<3;i++) await Comment(actor,review); await Comment(actor,otherReview);
        var page1=await Inbox(owner,"?pageSize=2"); var page2=await Inbox(owner,"?page=2&pageSize=2");
        Assert.Equal(3,page1.GetProperty("totalCount").GetInt32()); Assert.Equal(2,page1.GetProperty("items").GetArrayLength()); Assert.Single(page2.GetProperty("items").EnumerateArray());
        Assert.DoesNotContain(page2.GetProperty("items")[0].GetProperty("id").GetGuid(),page1.GetProperty("items").EnumerateArray().Select(x=>x.GetProperty("id").GetGuid()));
        NoContent(await owner.DeleteAsync("/api/v1/notifications",Ct)); NoContent(await owner.DeleteAsync("/api/v1/notifications",Ct));
        Assert.Equal(0,await Count(owner)); Assert.Equal(1,await Count(other));
        await Comment(actor,review); Assert.Equal(1,await Count(owner));
    }
    [Fact]
    public async Task Source_review_and_actor_or_recipient_erasure_cascade_notifications() {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f);
        var actorId=await SignIn(actor); var ownerId=await SignIn(owner); var (_,review)=await Review(owner); await Comment(actor,review);
        await using var db=Context();
        Assert.Equal(1,await db.Notifications.CountAsync(n=>n.RecipientId==ownerId,Ct));
        await db.Reviews.Where(x=>x.Id==review).ExecuteDeleteAsync(Ct);
        Assert.Equal(0,await db.Notifications.CountAsync(n=>n.RecipientId==ownerId,Ct));
        NoContent(await actor.PutAsync($"/api/v1/users/{ownerId}/follow",null,Ct));
        await db.Users.Where(x=>x.Id==actorId).ExecuteDeleteAsync(Ct);
        Assert.Equal(0,await db.Notifications.CountAsync(n=>n.ActorId==actorId||n.RecipientId==actorId,Ct));
        using var next=Client(f); await SignIn(next); NoContent(await next.PutAsync($"/api/v1/users/{ownerId}/follow",null,Ct));
        await db.Users.Where(x=>x.Id==ownerId).ExecuteDeleteAsync(Ct);
        Assert.Equal(0,await db.Notifications.CountAsync(n=>n.ActorId==ownerId||n.RecipientId==ownerId,Ct));
    }
    [Fact]
    public async Task Clear_statement_preserves_notifications_committed_after_its_snapshot() {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f);
        await SignIn(actor); var ownerId=await SignIn(owner); var (_,review)=await Review(owner); await Comment(actor,review);
        await using var blocker=Context(); await using var tx=await blocker.Database.BeginTransactionAsync(Ct);
        // Hold an existing row so the clear DELETE takes its snapshot and then blocks.
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM notifications WHERE recipient_id = {ownerId} FOR UPDATE",Ct);
        var clear=owner.DeleteAsync("/api/v1/notifications",Ct);
        await using var observer=Context();
        var deadline=DateTime.UtcNow.AddSeconds(10); var waiting=false;
        while(DateTime.UtcNow<deadline) {
            waiting=await observer.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query ILIKE '%DELETE FROM notifications%'").SingleAsync(Ct)>0;
            if(waiting) break; await Task.Delay(25,Ct);
        }
        Assert.True(waiting,"Clear statement should be waiting on the locked notification");
        await Comment(actor,review); await tx.CommitAsync(Ct); NoContent(await clear); Assert.Equal(1,await Count(owner));
    }
    [Fact]
    public async Task Export_contains_only_accessible_existing_notifications() {
        await using var f=Factory(); using var actor=Client(f); using var owner=Client(f);
        await SignIn(actor); var ownerId=await SignIn(owner); var (_,review)=await Review(owner); var comment=await Comment(actor,review);
        NoContent(await actor.PutAsync($"/api/v1/users/{ownerId}/follow",null,Ct));
        await using var db=Context(); await db.ReviewComments.Where(c=>c.Id==comment).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.IsHidden,true),Ct);
        var confirmed=await owner.PostAsJsonAsync("/api/v1/users/me/data/confirm",new {operation="export",password="Local-password-72"},Ct);
        Assert.Equal(HttpStatusCode.NoContent,confirmed.StatusCode);
        var export=await owner.PostAsJsonAsync("/api/v1/users/me/data/export",new {},Ct); Assert.Equal(HttpStatusCode.OK,export.StatusCode);
        using var zip=new ZipArchive(await export.Content.ReadAsStreamAsync(Ct));
        using var entry=zip.GetEntry("notifications.json")!.Open(); var json=await JsonDocument.ParseAsync(entry,cancellationToken:Ct);
        var exported=Assert.Single(json.RootElement.EnumerateArray()); Assert.Equal("follow",exported.GetProperty("kind").GetString());
        NoContent(await owner.DeleteAsync($"/api/v1/notifications/{exported.GetProperty("id").GetGuid()}",Ct));
        Assert.Equal(HttpStatusCode.NoContent,(await owner.PostAsJsonAsync("/api/v1/users/me/data/confirm",new {operation="export",password="Local-password-72"},Ct)).StatusCode);
        var again=await owner.PostAsJsonAsync("/api/v1/users/me/data/export",new {},Ct); Assert.Equal(HttpStatusCode.OK,again.StatusCode);
        using var laterZip=new ZipArchive(await again.Content.ReadAsStreamAsync(Ct)); using var later=laterZip.GetEntry("notifications.json")!.Open();
        var laterJson=await JsonDocument.ParseAsync(later,cancellationToken:Ct); Assert.Empty(laterJson.RootElement.EnumerateArray());
    }
}
