using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class ReviewCommentTests
{
    [Fact]
    public async Task Tied_pages_are_stable_and_count_only_the_requested_review()
    {
        await using var factory=Factory();using var client=Client(factory);await SignIn(client);var reviewId=await Review(client);var other=await Review(client);
        var ids=new List<Guid>();for(var i=0;i<3;i++)ids.Add((await Create(client,reviewId,"Comment "+i)).GetProperty("id").GetGuid());
        await Create(client,other,"Unrelated");
        await using var db=Context();var tie=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);
        await db.ReviewComments.Where(x=>x.ReviewId==reviewId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.CreatedAt,tie),Ct);
        var expected=ids.Order().ToArray();
        for(var page=1;page<=3;page++)
        {
            var result=await Json(await client.GetAsync(List(reviewId)+$"?page={page}&pageSize=1",Ct));
            Assert.Equal(3,result.GetProperty("totalCount").GetInt32());Assert.Equal(page,result.GetProperty("page").GetInt32());Assert.Equal(1,result.GetProperty("pageSize").GetInt32());
            Assert.Equal(expected[page-1],Assert.Single(result.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        }
        Assert.Empty((await Json(await client.GetAsync(List(reviewId)+"?page=4&pageSize=1",Ct))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Body_check_and_foreign_keys_reject_direct_invalid_writes()
    {
        await using var factory=Factory();using var client=Client(factory);await SignIn(client);var reviewId=await Review(client);
        // Single-letter bodies catch accidental trimming of literal t/n/r instead of controls.
        var comment=await Create(client,reviewId,"t");var id=comment.GetProperty("id").GetGuid();
        await using var db=Context();
        foreach(var invalid in new[] { (Body:"",Code:PostgresErrorCodes.CheckViolation),
            (Body:" \t\n\r ",Code:PostgresErrorCodes.CheckViolation),
            (Body:new string('x',2001),Code:PostgresErrorCodes.StringDataRightTruncation) })
        {
            var error=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE review_comments SET body={invalid.Body} WHERE id={id}",Ct));
            Assert.Equal(invalid.Code,error.SqlState);
        }
        var missing=Guid.NewGuid();
        var orphan=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE review_comments SET review_id={missing} WHERE id={id}",Ct));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,orphan.SqlState);
        orphan=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE review_comments SET user_id={missing} WHERE id={id}",Ct));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation,orphan.SqlState);
        Assert.Equal("t",(await Json(await client.GetAsync(Detail(id),Ct))).GetProperty("body").GetString());
        await db.Database.MigrateAsync(Ct);await db.Database.MigrateAsync(Ct);
        Assert.False(db.Database.HasPendingModelChanges());Assert.Single(await db.Database.GetAppliedMigrationsAsync(Ct),x=>x.EndsWith("_AddReviewComments",StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deleting_review_or_comment_author_cascades_only_related_comments()
    {
        await using var factory=Factory();using var owner=Client(factory);using var writer=Client(factory);await SignIn(owner);var writerId=await SignIn(writer);
        var reviewId=await Review(owner);var second=await Review(owner);
        var deletedWithParent=(await Create(writer,reviewId)).GetProperty("id").GetGuid();
        var deletedWithAuthor=(await Create(writer,second)).GetProperty("id").GetGuid();
        var survivor=(await Create(owner,second)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,(await owner.DeleteAsync($"/api/v1/reviews/{reviewId}",Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await owner.GetAsync(Detail(deletedWithParent),Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await owner.GetAsync(List(reviewId),Ct)).StatusCode);
        await using var db=Context();await db.Users.Where(x=>x.Id==writerId).ExecuteDeleteAsync(Ct);
        Assert.False(await db.ReviewComments.AnyAsync(x=>x.Id==deletedWithAuthor,Ct));
        Assert.True(await db.ReviewComments.AnyAsync(x=>x.Id==survivor,Ct));Assert.True(await db.Reviews.AnyAsync(x=>x.Id==second,Ct));
        await Json(await owner.GetAsync(Detail(survivor),Ct));
    }

    // Delete exactly before INSERT on a separate connection, after HTTP authentication.
    private sealed class DeleteBeforeCommentInsert(string connectionString,bool deleteAuthor) : DbCommandInterceptor
    {
        public Guid TargetId { get; set; }
        public bool Attempted { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,
            InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
        {
            if(Attempted||!command.CommandText.Contains("INSERT INTO review_comments",StringComparison.Ordinal))return result;
            Attempted=true;
            await using var connection=new NpgsqlConnection(connectionString);await connection.OpenAsync(cancellationToken);
            var sql=deleteAuthor?"DELETE FROM users WHERE id=@id":"DELETE FROM reviews WHERE id=@id";
            await using var deletion=new NpgsqlCommand(sql,connection);deletion.Parameters.AddWithValue("id",TargetId);await deletion.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Parent_or_author_deleted_before_insert_returns_404_without_orphan(bool deleteAuthor)
    {
        var gate=new DeleteBeforeCommentInsert(database.ConnectionString,deleteAuthor);
        await using var factory=new ApiFactory(connectionString:database.ConnectionString,configureServices:s=>s.AddDbContext<RateFreakDbContext>(o=>o.AddInterceptors(gate)));
        using var owner=Client(factory);using var author=Client(factory);await SignIn(owner);var authorId=await SignIn(author);var reviewId=await Review(owner);
        gate.TargetId=deleteAuthor?authorId:reviewId;
        Assert.Equal(HttpStatusCode.NotFound,(await author.PostAsJsonAsync(List(reviewId),new { body="Racing write" },Ct)).StatusCode);
        Assert.True(gate.Attempted);await using var db=Context();Assert.False(await db.ReviewComments.AnyAsync(x=>x.ReviewId==reviewId,Ct));
    }

    [Fact]
    public async Task Comment_limits_share_social_budgets_keep_no_store_and_isolate_accounts()
    {
        await using var factory=Factory();using var first=Client(factory);using var second=Client(factory);await SignIn(first);await SignIn(second);var reviewId=await Review(first);
        for(var i=0;i<30;i++)await Create(first,reviewId);
        var limited=await first.PostAsJsonAsync(List(reviewId),new { body="Over budget" },Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests,limited.StatusCode);Assert.NotNull(limited.Headers.RetryAfter);Assert.True(limited.Headers.CacheControl?.NoStore);
        await Create(second,reviewId);
        // Follows and comments share the same mutation budget.
        Assert.Equal(HttpStatusCode.TooManyRequests,(await first.PutAsync($"/api/v1/users/{Guid.NewGuid()}/follow",null,Ct)).StatusCode);
        for(var i=0;i<60;i++)Assert.Equal(HttpStatusCode.OK,(await first.GetAsync(List(reviewId),Ct)).StatusCode);
        limited=await first.GetAsync(List(reviewId),Ct);Assert.Equal(HttpStatusCode.TooManyRequests,limited.StatusCode);Assert.NotNull(limited.Headers.RetryAfter);Assert.True(limited.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await first.GetAsync($"/api/v1/users/{Guid.NewGuid()}/followers",Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await first.GetAsync("/api/v1/media",Ct)).StatusCode);
    }

    [Fact]
    public async Task Development_OpenAPI_documents_every_comment_route()
    {
        await using var factory=new ApiFactory(environment:"Development",connectionString:database.ConnectionString);using var client=Client(factory);
        var doc=await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json",Ct);var paths=doc.GetProperty("paths");
        var list=paths.GetProperty("/api/v1/reviews/{reviewId}/comments");Assert.True(list.TryGetProperty("get",out _));Assert.True(list.TryGetProperty("post",out _));
        var detail=paths.GetProperty("/api/v1/comments/{id}");Assert.True(detail.TryGetProperty("get",out _));Assert.True(detail.TryGetProperty("patch",out _));Assert.True(detail.TryGetProperty("delete",out _));
    }
    private sealed class DeleteAfterCommentWrite(string connectionString,bool deleteAuthor) : DbCommandInterceptor
    {
        public Guid TargetId { get; set; }
        public string Match { get; set; }="disabled";
        public bool Blocked { get; private set; }
        private async Task Delete(DbCommand command,CancellationToken ct)
        {
            if(Blocked||!command.CommandText.Contains(Match,StringComparison.Ordinal))return;
            await using var connection=new NpgsqlConnection(connectionString);await connection.OpenAsync(ct);
            await using var timeout=new NpgsqlCommand("SET lock_timeout='250ms'",connection);await timeout.ExecuteNonQueryAsync(ct);
            await using var deletion=new NpgsqlCommand(deleteAuthor?"DELETE FROM users WHERE id=@id":"DELETE FROM reviews WHERE id=@id",connection);
            deletion.Parameters.AddWithValue("id",TargetId);
            try { await deletion.ExecuteNonQueryAsync(ct); }
            catch(PostgresException error) when(error.SqlState==PostgresErrorCodes.LockNotAvailable) { Blocked=true; }
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData data,DbDataReader result,CancellationToken ct=default)
        { await Delete(command,ct);return result; }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command,CommandExecutedEventData data,int result,CancellationToken ct=default)
        { await Delete(command,ct);return result; }
    }
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public async Task Parent_or_author_cannot_disappear_between_comment_write_and_response(bool update,bool deleteAuthor)
    {
        var gate=new DeleteAfterCommentWrite(database.ConnectionString,deleteAuthor);
        await using var factory=new ApiFactory(connectionString:database.ConnectionString,configureServices:s=>s.AddDbContext<RateFreakDbContext>(o=>o.AddInterceptors(gate)));
        using var owner=Client(factory);using var author=Client(factory);await SignIn(owner);var authorId=await SignIn(author);var reviewId=await Review(owner);
        var id=update?(await Create(author,reviewId)).GetProperty("id").GetGuid():Guid.Empty;
        gate.TargetId=deleteAuthor?authorId:reviewId;gate.Match=update?"UPDATE review_comments":"INSERT INTO review_comments";
        var response=update?await author.PatchAsJsonAsync(Detail(id),new { body="Changed" },Ct):await author.PostAsJsonAsync(List(reviewId),new { body="Changed" },Ct);
        var result=await Json(response,update?HttpStatusCode.OK:HttpStatusCode.Created);
        Assert.True(gate.Blocked);Assert.Equal("Changed",result.GetProperty("body").GetString());
        Assert.Equal(authorId,result.GetProperty("author").GetProperty("id").GetGuid());Assert.Equal(reviewId,result.GetProperty("reviewId").GetGuid());
    }

}
