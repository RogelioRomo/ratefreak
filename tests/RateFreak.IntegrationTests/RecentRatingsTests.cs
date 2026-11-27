using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed class RecentRatingsTests(AccountDatabase database) : IClassFixture<AccountDatabase>
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
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }



    private const string Route="/api/v1/users/me/ratings/recent";
    private async Task<Guid> Rate(Guid user, DateTimeOffset at, bool hidden=false, bool custom=false, bool archived=false)
    {
        await using var db=Context();
        var item=custom ? MediaItem.CreateCustom(user,"Private rated item",null,Category.ForMediaType("movie")) : new MediaItem("movie","Rated title");
        db.Add(item); db.Add(new RateFreak.Domain.PersonalMedia.Rating(user,item.Id,0)); await db.SaveChangesAsync(Ct);
        await db.Ratings.Where(x=>x.UserId==user && x.MediaItemId==item.Id).ExecuteUpdateAsync(x=>x.SetProperty(r=>r.UpdatedAt,at),Ct);
        await db.MediaItems.Where(x=>x.Id==item.Id).ExecuteUpdateAsync(x=>x.SetProperty(m=>m.IsHidden,hidden).SetProperty(m=>m.IsArchived,archived),Ct);
        return item.Id;
    }
    [Fact]
    public async Task Recent_ratings_are_owner_only_bounded_ordered_and_do_not_require_library_entries()
    {
        await using var factory=Factory(); using var owner=Client(factory); using var other=Client(factory); using var anonymous=Client(factory);
        var user=await SignIn(owner); var otherUser=await SignIn(other); var at=DateTimeOffset.UtcNow.AddDays(-1);
        Assert.Empty((await Json(await owner.GetAsync(Route,Ct))).GetProperty("items").EnumerateArray());
        var ids=new List<Guid>(); for(var i=0;i<14;i++) ids.Add(await Rate(user,at.AddMinutes(i)));
        var privateId=await Rate(user,at.AddMinutes(20),custom:true);
        await Rate(user,at.AddMinutes(30),hidden:true); await Rate(user,at.AddMinutes(31),archived:true);
        var otherId=await Rate(otherUser,at.AddMinutes(40));
        var response=await owner.GetAsync(Route,Ct); var data=await Json(response);
        Assert.Contains("no-store",response.Headers.CacheControl!.ToString());
        var rows=data.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(12,rows.Length);
        Assert.Equal(new[]{privateId}.Concat(ids.AsEnumerable().Reverse().Take(11)),rows.Select(x=>x.GetProperty("media").GetProperty("id").GetGuid()));
        Assert.Equal(0,rows[0].GetProperty("score").GetDecimal());
        Assert.Equal(otherId,(await Json(await other.GetAsync(Route,Ct))).GetProperty("items")[0].GetProperty("media").GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(Route,Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.GetAsync(Route+"?userId="+otherUser,Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await owner.PutAsJsonAsync($"/api/v1/media/{ids[0]}/rating",new {score=4.5},Ct)).StatusCode);
        Assert.Equal(ids[0],(await Json(await owner.GetAsync(Route,Ct))).GetProperty("items")[0].GetProperty("media").GetProperty("id").GetGuid());
        await owner.DeleteAsync($"/api/v1/media/{ids[0]}/rating",Ct);
        Assert.DoesNotContain((await Json(await owner.GetAsync(Route,Ct))).GetProperty("items").EnumerateArray(),x=>x.GetProperty("media").GetProperty("id").GetGuid()==ids[0]);
    }
}
