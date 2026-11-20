using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RateFreak.Application.PersonalMedia;
using Npgsql;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed class LibraryHistoryTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private RateFreakDbContext Context() => new(new DbContextOptionsBuilder<RateFreakDbContext>()
        .UseNpgsql(database.ConnectionString).Options);
    private ApiFactory Factory() => new(connectionString: database.ConnectionString);
    private static HttpClient Client(ApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static async Task Token(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf", Ct);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.GetProperty("requestToken").GetString());
    }
    private static async Task<Guid> SignIn(HttpClient client)
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var email = tag + "@example.test";
        await Token(client);
        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
            { email, password = "Local-password-72", handle = "user_" + tag, displayName = "Reader" }, Ct);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var account = await registration.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "Local-password-72" }, Ct)).StatusCode);
        await Token(client);
        return account.GetProperty("id").GetGuid();
    }
    private async Task<Guid> Media(string type = "movie", string title = "Phase four test")
    {
        await using var db = Context();
        var item = new MediaItem(type, title);
        db.Add(item); await db.SaveChangesAsync(Ct); return item.Id;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }



    private async Task<Guid> Seed(Guid user, DateOnly? completed, string status = "completed", string type = "movie", decimal? score = null, bool hidden = false)
    {
        await using var db = Context();
        var media = type == "custom" ? MediaItem.CreateCustom(user, "Custom history", null, Category.ForMediaType("movie")) : new MediaItem(type, "History title");
        db.Add(media); db.Add(new RateFreak.Domain.PersonalMedia.LibraryEntry(user, media.Id, status, null, completed));
        if (score is not null) db.Add(new RateFreak.Domain.PersonalMedia.Rating(user, media.Id, score.Value));
        await db.SaveChangesAsync(Ct);
        if (hidden) await db.MediaItems.Where(x => x.Id == media.Id).ExecuteUpdateAsync(x => x.SetProperty(m => m.IsHidden, true), Ct);
        return media.Id;
    }
    private static async Task<JsonElement> History(HttpClient client, string query = "") => await Json(await client.GetAsync("/api/v1/users/me/library/history" + (query.Length == 0 ? "" : "?" + query), Ct));

    [Fact]
    public async Task Eligibility_counts_months_and_ratings_are_private_and_consistent()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory);
        var user = await SignIn(owner); var otherUser = await SignIn(other);
        var jan = await Seed(user, new(2025,1,31), score:0);
        var feb = await Seed(user, new(2025,2,28), type:"book");
        var custom = await Seed(user, new(2025,3,1), type:"custom");
        await Seed(user, new(2024,2,29)); await Seed(user, null);
        await Seed(user, new(2025,4,1), status:"paused"); await Seed(user,new(2025,5,1),hidden:true);
        await Seed(otherUser,new(2030,1,1));
        await using (var db = Context()) { db.Add(new RateFreak.Domain.PersonalMedia.Rating(otherUser, feb, 5)); await db.SaveChangesAsync(Ct); }
        var result = await History(owner,"year=2025");
        Assert.Equal(2025,result.GetProperty("year").GetInt32()); Assert.Equal(3,result.GetProperty("annualTotal").GetInt32());
        Assert.Equal(3,result.GetProperty("totalCount").GetInt32()); Assert.Equal(1,result.GetProperty("missingDateCount").GetInt32());
        Assert.Equal(new[]{2025,2024},result.GetProperty("availableYears").EnumerateArray().Select(x=>x.GetInt32()));
        var months = result.GetProperty("months").EnumerateArray().ToArray(); Assert.Equal(12,months.Length);
        Assert.Equal(new[]{1,1,1,0,0,0,0,0,0,0,0,0},months.Select(x=>x.GetProperty("count").GetInt32()));
        Assert.Equal(Enumerable.Range(1,12),months.Select(x=>x.GetProperty("month").GetInt32()));
        var rows=result.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[]{custom,feb,jan},rows.Select(x=>x.GetProperty("media").GetProperty("id").GetGuid()));
        Assert.Equal(JsonValueKind.Null,rows[1].GetProperty("ownRating").ValueKind);
        Assert.Equal(0m,rows[2].GetProperty("ownRating").GetDecimal());
        var selected=await History(owner,"year=2025&month=2");
        Assert.Equal(1,selected.GetProperty("totalCount").GetInt32());Assert.Equal(3,selected.GetProperty("annualTotal").GetInt32());
        Assert.Equal("2025-02-28",selected.GetProperty("items")[0].GetProperty("completedAt").GetString());
        Assert.Equal(1,(await History(other,"year=2030")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Default_and_empty_year_choices_are_resolved_from_eligible_data()
    {
        await using var factory=Factory();using var client=Client(factory);var user=await SignIn(client);
        var before=DateTime.UtcNow.Year;var empty=await History(client);var after=DateTime.UtcNow.Year;
        Assert.Contains(empty.GetProperty("year").GetInt32(),new[]{before,after});
        Assert.Equal(0,empty.GetProperty("annualTotal").GetInt32());Assert.Equal(12,empty.GetProperty("months").GetArrayLength());
        await Seed(user,new(2099,5,1));await Seed(user,new(2100,1,1),status:"planned");
        Assert.Equal(2099,(await History(client)).GetProperty("year").GetInt32());
        var explicitYear=await History(client,"year=2000");Assert.Equal(2000,explicitYear.GetProperty("year").GetInt32());
        Assert.Equal(0,explicitYear.GetProperty("totalCount").GetInt32());
        Assert.Contains(2000,explicitYear.GetProperty("availableYears").EnumerateArray().Select(x=>x.GetInt32()));
    }

    [Fact]
    public async Task Full_month_counts_and_equal_date_pages_have_no_duplicates()
    {
        await using var factory=Factory();using var client=Client(factory);var user=await SignIn(client);var ids=new List<Guid>();
        for(var i=0;i<25;i++)ids.Add(await Seed(user,new(2024,2,29)));
        var first=await History(client,"year=2024&pageSize=20");var second=await History(client,"year=2024&pageSize=20&page=2");
        Assert.Equal(25,first.GetProperty("totalCount").GetInt32());Assert.Equal(25,second.GetProperty("annualTotal").GetInt32());
        Assert.Equal(25,first.GetProperty("months")[1].GetProperty("count").GetInt32());
        var actual=first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).Select(x=>x.GetProperty("media").GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(ids.OrderBy(x=>x.ToString(),StringComparer.Ordinal),actual);Assert.Equal(25,actual.Distinct().Count());
    }

    [Theory]
    [InlineData(1,1,1)]
    [InlineData(2024,2,29)]
    [InlineData(9999,12,31)]
    public async Task Date_only_year_and_month_bounds_do_not_overflow(int year,int month,int day)
    {
        await using var factory=Factory();using var client=Client(factory);var user=await SignIn(client);
        await Seed(user,new(year,month,day));
        await using var scope=factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ILibraryHistoryService>().GetAsync(user,new(year,month,1,20),Ct);
        var result=await History(client,$"year={year}&month={month}");
        Assert.Equal(1,result.GetProperty("totalCount").GetInt32());Assert.Equal($"{year:D4}-{month:D2}-{day:D2}",result.GetProperty("items")[0].GetProperty("completedAt").GetString());
    }

    [Fact]
    public async Task Existing_library_edits_status_changes_and_removals_update_the_recap()
    {
        await using var factory=Factory();using var client=Client(factory);var user=await SignIn(client);var id=await Seed(user,new(2025,1,1));var path=$"/api/v1/users/me/library/{id}";
        await Json(await client.PutAsJsonAsync(path,new {status="completed",completedAt="2024-12-31"},Ct));
        Assert.Equal(0,(await History(client,"year=2025")).GetProperty("totalCount").GetInt32());Assert.Equal(1,(await History(client,"year=2024&month=12")).GetProperty("totalCount").GetInt32());
        await Json(await client.PutAsJsonAsync(path,new {status="paused",completedAt="2024-12-31"},Ct));
        Assert.Equal(0,(await History(client,"year=2024")).GetProperty("totalCount").GetInt32());
        await Json(await client.PutAsJsonAsync(path,new {status="completed",completedAt="2024-12-31"},Ct));
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync(path,Ct)).StatusCode);
        Assert.Equal(0,(await History(client,"year=2024")).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("month=2")]
    [InlineData("year=0")]
    [InlineData("year=10000")]
    [InlineData("year=word")]
    [InlineData("year=2025&month=0")]
    [InlineData("year=2025&month=13")]
    [InlineData("year=2025&year=2024")]
    [InlineData("year=2025&month=1&month=2")]
    [InlineData("page=0")]
    [InlineData("page=10001")]
    [InlineData("pageSize=101")]
    [InlineData("q=anything")]
    public async Task Invalid_history_query_is_rejected(string query)
    {
        await using var factory=Factory();using var client=Client(factory);await SignIn(client);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/v1/users/me/library/history?"+query,Ct)).StatusCode);
    }

    [Fact]
    public async Task Reads_require_identity_and_are_never_stored_in_http_caches()
    {
        await using var factory=Factory();using var client=Client(factory);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/users/me/library/history",Ct)).StatusCode);
        await SignIn(client);var response=await client.GetAsync("/api/v1/users/me/library/history",Ct);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private sealed class DeleteDuringRead(string connectionString) : DbCommandInterceptor
    {
        public Guid UserId {get;set;} public bool Armed {get;set;} public bool Attempted {get;private set;}
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken cancellationToken=default)
        {
            if(!Armed||Attempted||!command.CommandText.Contains("user_media",StringComparison.Ordinal))return result;
            Attempted=true;
            await using var connection=new NpgsqlConnection(connectionString);await connection.OpenAsync(cancellationToken);
            await using var deletion=new NpgsqlCommand("DELETE FROM user_media WHERE user_id = @u",connection);deletion.Parameters.AddWithValue("u",UserId);await deletion.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }
    [Fact]
    public async Task Aggregates_and_rows_share_one_database_snapshot()
    {
        var gate=new DeleteDuringRead(database.ConnectionString);
        await using var factory=new ApiFactory(connectionString:database.ConnectionString,configureServices:services=>services.AddDbContext<RateFreakDbContext>(options=>options.AddInterceptors(gate)));
        using var client=Client(factory);gate.UserId=await SignIn(client);await Seed(gate.UserId,new(2025,6,1));gate.Armed=true;
        var result=await History(client,"year=2025");Assert.True(gate.Attempted);
        Assert.Equal(1,result.GetProperty("annualTotal").GetInt32());Assert.Equal(1,result.GetProperty("totalCount").GetInt32());Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal(0,(await History(client,"year=2025")).GetProperty("totalCount").GetInt32());
    }
}
