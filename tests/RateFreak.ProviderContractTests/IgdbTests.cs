using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Providers.Igdb;

namespace RateFreak.ProviderContractTests;

public sealed partial class IgdbTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string TokenJson = """{"access_token":"fake-app-token","expires_in":3600,"token_type":"bearer"}""";
    private const string Game = """{"id":1942,"name":" The Witcher 3 ","game_type":0,"summary":"An adventure","first_release_date":1431993600,"platforms":[{"id":6,"name":"PC"}],"genres":[{"id":12,"name":"RPG"}],"cover":{"image_id":"co1abc"},"rating":99}""";
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) => send(request,ct);
    }
    private static HttpResponseMessage Json(string text,HttpStatusCode status=HttpStatusCode.OK) => new(status) { Content=new StringContent(text,Encoding.UTF8,"application/json") };
    private static IgdbMediaProvider Provider(HttpClient games,HttpClient auth,IgdbSession session,TimeProvider? clock=null,bool enabled=true) =>
        new(games,auth,Options.Create(new IgdbOptions { Enabled=enabled,ClientId="fakeclient",ClientSecret="fakesecret" }),session,clock??TimeProvider.System);

    [Fact]
    public async Task Details_exchange_app_credentials_in_body_and_map_bounded_core_fields()
    {
        using var authHandler=new Handler(async (request,ct)=>
        {
            Assert.Equal("https://id.twitch.tv/oauth2/token",request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post,request.Method);Assert.Null(request.Headers.Authorization);
            Assert.Equal("application/x-www-form-urlencoded",request.Content!.Headers.ContentType!.MediaType);
            var body=await request.Content.ReadAsStringAsync(ct);
            Assert.Contains("client_id=fakeclient",body);Assert.Contains("client_secret=fakesecret",body);Assert.Contains("grant_type=client_credentials",body);
            return Json(TokenJson);
        });
        using var gamesHandler=new Handler(async (request,ct)=>
        {
            Assert.Equal("https://api.igdb.com/v4/games",request.RequestUri!.AbsoluteUri);Assert.Equal(HttpMethod.Post,request.Method);
            Assert.Equal("Bearer fake-app-token",request.Headers.Authorization!.ToString());Assert.Equal("fakeclient",Assert.Single(request.Headers.GetValues("Client-ID")));
            var body=await request.Content!.ReadAsStringAsync(ct);Assert.Contains("where id = 1942;",body);Assert.Contains("game_type",body);Assert.DoesNotContain("category",body);
            return Json("["+Game+"]");
        });
        using var games=new HttpClient(gamesHandler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        var item=await Provider(games,auth,session).GetDetailsAsync("0001942","video_game",Ct);
        Assert.NotNull(item);Assert.Equal("igdb",item.Provider);Assert.Equal("1942",item.ExternalId);Assert.Equal("video_game",item.Type);Assert.Equal("The Witcher 3",item.Title);
        Assert.Equal("An adventure",item.Description);Assert.Null(item.ReleaseDate);Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/co1abc.jpg",item.ImageReference);
        Assert.Equal(0,item.Metadata.GetProperty("gameType").GetInt32());Assert.Equal(1431993600,item.Metadata.GetProperty("firstReleaseTimestamp").GetInt64());
        Assert.Equal("PC",item.Metadata.GetProperty("platforms")[0].GetProperty("name").GetString());Assert.False(item.Metadata.TryGetProperty("rating",out _));
    }

    [Fact]
    public async Task Search_escapes_literal_and_keeps_scope_and_offset()
    {
        using var handler=new Handler(async (request,ct)=>
        {
            var body=await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("search \"a\\\"; where id=1; \\\\b\";",body);
            Assert.Contains("game_type = (0,8,9)",body);Assert.Contains("version_parent = null",body);Assert.Contains("limit 20; offset 20;",body);
            return Json("["+Game+","+Game.Replace("1942","1943").Replace("\"game_type\":0","\"game_type\":1")+"]");
        });
        using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        var page=await Provider(games,auth,session).SearchAsync("a\"; where id=1; \\b","video_game",2,Ct);
        Assert.Equal(2,page.Page);Assert.Single(page.Items);
    }

    [Theory]
    [InlineData(0,true)] [InlineData(8,true)] [InlineData(9,true)] [InlineData(1,false)] [InlineData(2,false)] [InlineData(3,false)] [InlineData(11,false)] [InlineData(999,false)]
    public async Task Only_main_remakes_remasters_are_importable(int type,bool included)
    {
        using var handler=new Handler((_,_)=>Task.FromResult(Json("["+Game.Replace("\"game_type\":0","\"game_type\":"+type)+"]")));
        using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        Assert.Equal(included,await Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct) is not null);
    }

    [Theory]
    [InlineData("[]",false)]
    [InlineData("[{\"id\":1942,\"name\":\"Edition\",\"game_type\":0,\"version_parent\":1}]",false)]
    [InlineData("[{\"id\":1942,\"name\":\"Minimal\",\"game_type\":0}]",true)]
    public async Task Empty_excluded_and_minimal_details_have_expected_semantics(string body,bool present)
    {
        using var handler=new Handler((_,_)=>Task.FromResult(Json(body)));using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        Assert.Equal(present,await Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct) is not null);
    }

    [Theory]
    [InlineData("{}")] [InlineData("not json")] [InlineData("[{}]")]
    [InlineData("[{\"id\":1943,\"name\":\"Wrong excluded item\",\"game_type\":1}]")]
    [InlineData("[{\"id\":1942,\"id\":1,\"name\":\"x\",\"game_type\":0}]")]
    [InlineData("[{\"id\":1942,\"name\":\"\\uD800\",\"game_type\":0}]")]
    [InlineData("[{\"id\":1942,\"name\":\"x\",\"game_type\":\"0\"}]")]
    public async Task Malformed_and_mismatched_responses_are_sanitized(string body)
    {
        using var handler=new Handler((_,_)=>Task.FromResult(Json(body)));using var authHandler=new Handler((_,_)=>Task.FromResult(Json(TokenJson)));
        using var games=new HttpClient(handler);using var auth=new HttpClient(authHandler);using var session=new IgdbSession(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(()=>Provider(games,auth,session).GetDetailsAsync("1942","video_game",Ct))).Failure);
    }

    [Theory]
    [InlineData("igdb","video_game","001942",true)] [InlineData("igdb","album","1942",false)] [InlineData("tmdb","video_game","1942",false)]
    [InlineData("igdb","video_game","0",false)] [InlineData("igdb","video_game","2147483648",false)] [InlineData("igdb","video_game","https://evil.test",false)]
    public void Provider_identity_is_validated_before_access(string provider,string type,string id,bool valid) => Assert.Equal(valid,MediaProviderInput.TryIdentity(provider,type,id,out _));
    [Fact]
    public void Numeric_game_identity_does_not_invent_a_public_slug_link()
    {
        Assert.True(MediaProviderInput.TryIdentity("igdb","video_game","001942",out var identity));
        Assert.Equal("1942",identity.ExternalId);Assert.Equal("game",identity.Namespace);
        Assert.Null(identity.ExternalUrl);
    }

}
