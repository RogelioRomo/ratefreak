using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed partial class ReviewCommentTests
{
    private sealed class InsertAfterCount(string connectionString) : DbCommandInterceptor
    {
        public Guid ReviewId { get; set; }
        public Guid UserId { get; set; }
        public bool Inserted { get; private set; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData data,DbDataReader result,CancellationToken ct=default)
        {
            if(Inserted||!command.CommandText.Contains("count(*)",StringComparison.Ordinal)||!command.CommandText.Contains("review_comments",StringComparison.Ordinal))return result;
            await using var connection=new NpgsqlConnection(connectionString);await connection.OpenAsync(ct);
            await using var insertion=new NpgsqlCommand("INSERT INTO review_comments (id,user_id,review_id,body,is_spoiler,created_at,updated_at) VALUES (@id,@user,@review,'Concurrent comment',false,now(),now())",connection);
            insertion.Parameters.AddWithValue("id",Guid.NewGuid());insertion.Parameters.AddWithValue("user",UserId);insertion.Parameters.AddWithValue("review",ReviewId);
            await insertion.ExecuteNonQueryAsync(ct);Inserted=true;return result;
        }
    }
    [Fact]
    public async Task List_keeps_count_and_page_in_one_snapshot_during_concurrent_creation()
    {
        var interceptor=new InsertAfterCount(database.ConnectionString);
        await using var factory=new ApiFactory(connectionString:database.ConnectionString,configureServices:s=>s.AddDbContext<RateFreakDbContext>(o=>o.AddInterceptors(interceptor)));
        using var client=Client(factory);interceptor.UserId=await SignIn(client);interceptor.ReviewId=await Review(client);
        await Create(client,interceptor.ReviewId,"Existing comment");
        var page=await Json(await client.GetAsync(List(interceptor.ReviewId),Ct));Assert.True(interceptor.Inserted);
        Assert.Equal(1,page.GetProperty("totalCount").GetInt32());Assert.Equal("Existing comment",Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("body").GetString());
        await using var db=Context();Assert.Equal(2,await db.ReviewComments.CountAsync(x=>x.ReviewId==interceptor.ReviewId,Ct));
    }
}
