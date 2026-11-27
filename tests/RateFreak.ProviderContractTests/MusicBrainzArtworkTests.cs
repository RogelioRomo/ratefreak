using System.Text.Json;

namespace RateFreak.ProviderContractTests;

public sealed partial class MusicBrainzTests
{
    private const string Release = "df86f3ac-6da2-3e5c-b4ba-5f8abb29ce51";
    private const string Group = "3c0ed061-55d0-3cf2-bdb8-d88425e0f3ea";
    private static string SongWithReleases(params object[] releases)
    {
        var row = JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(Recording())!;
        row["releases"] = JsonSerializer.SerializeToElement(releases);
        return JsonSerializer.Serialize(row);
    }
    private static object SongRelease(string id=Release, string group=Group, string title="Year Zero", string primary="Album", string status="Official", string date="2007-04-13", string[]? secondary=null) => new Dictionary<string,object>
    {
        ["id"]=id, ["title"]=title, ["date"]=date, ["status"]=status,
        ["release-group"]=new Dictionary<string,object> { ["id"]=group, ["title"]=title, ["primary-type"]=primary, ["secondary-types"]=secondary ?? [] }
    };

    [Fact]
    public async Task Song_details_request_associated_releases_and_use_album_cover_without_changing_recording_identity()
    {
        using var handler=new Handler((request,_) =>
        {
            Assert.Contains("artist-credits+releases+release-groups",Uri.UnescapeDataString(request.RequestUri!.Query));
            return Task.FromResult(Json(SongWithReleases(SongRelease())));
        });
        using var client=new HttpClient(handler); using var gate=new Infrastructure.Providers.MusicBrainz.MusicBrainzRequestGate(TimeProvider.System);
        var song=await Provider(client,gate).GetDetailsAsync(Id,"song",Ct);
        Assert.NotNull(song); Assert.Equal(Id,song.ExternalId); Assert.Equal("song",song.Type);
        Assert.Equal($"https://coverartarchive.org/release-group/{Group}/front-500",song.ImageReference);
        var release=song.Metadata.GetProperty("release"); Assert.Equal(Release,release.GetProperty("id").GetString());
        Assert.Equal(Group,release.GetProperty("releaseGroupId").GetString()); Assert.Equal("Year Zero",release.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Song_search_prefers_original_album_over_single_compilation_or_bootleg_in_any_response_order(bool reverse)
    {
        object[] releases=[SongRelease(id:"11111111-1111-1111-1111-111111111111",primary:"Single",date:"2007-03"),
            SongRelease(id:"22222222-2222-2222-2222-222222222222",secondary:["Compilation"],date:"2001"),
            SongRelease(id:"33333333-3333-3333-3333-333333333333",status:"Bootleg",date:"1999"),SongRelease()];
        if(reverse) Array.Reverse(releases);
        using var handler=new Handler((_,_)=>Task.FromResult(Json("{\"recordings\":["+SongWithReleases(releases)+"]}")));
        using var client=new HttpClient(handler); using var gate=new Infrastructure.Providers.MusicBrainz.MusicBrainzRequestGate(TimeProvider.System);
        var song=Assert.Single((await Provider(client,gate).SearchAsync("Survivalism","song",1,Ct)).Items);
        Assert.Equal(Release,song.Metadata.GetProperty("release").GetProperty("id").GetString());
        Assert.Equal($"https://coverartarchive.org/release-group/{Group}/front-500",song.ImageReference);
    }

    [Fact]
    public async Task Song_search_uses_exact_release_cover_when_group_is_absent()
    {
        using var handler=new Handler((_,_)=>Task.FromResult(Json("{\"recordings\":["+SongWithReleases(new {id=Release,title="Year Zero",status="Official"})+"]}")));
        using var client=new HttpClient(handler); using var gate=new Infrastructure.Providers.MusicBrainz.MusicBrainzRequestGate(TimeProvider.System);
        var song=Assert.Single((await Provider(client,gate).SearchAsync("Survivalism","song",1,Ct)).Items);
        Assert.Equal($"https://coverartarchive.org/release/{Release}/front-500",song.ImageReference);
    }

    [Fact]
    public async Task Song_uses_a_reported_front_cover_from_the_actual_release()
    {
        var release=(Dictionary<string,object>)SongRelease(); release["cover-art-archive"]=new {front=true};
        using var handler=new Handler((_,_)=>Task.FromResult(Json(SongWithReleases(release))));
        using var client=new HttpClient(handler); using var gate=new Infrastructure.Providers.MusicBrainz.MusicBrainzRequestGate(TimeProvider.System);
        var song=await Provider(client,gate).GetDetailsAsync(Id,"song",Ct);
        Assert.Equal($"https://coverartarchive.org/release/{Release}/front-500",song!.ImageReference);
    }

    [Fact]
    public async Task Optional_release_processing_is_bounded()
    {
        var releases=Enumerable.Repeat<object>(new {},100).Append(SongRelease()).ToArray();
        using var handler=new Handler((_,_)=>Task.FromResult(Json(SongWithReleases(releases))));
        using var client=new HttpClient(handler); using var gate=new Infrastructure.Providers.MusicBrainz.MusicBrainzRequestGate(TimeProvider.System);
        var song=await Provider(client,gate).GetDetailsAsync(Id,"song",Ct);
        Assert.NotNull(song); Assert.Null(song.ImageReference);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Equal_album_candidates_use_stable_id_tiebreak(bool reverse)
    {
        const string earlierId="11111111-1111-1111-1111-111111111111";
        object[] releases=[SongRelease(),SongRelease(id:earlierId)]; if(reverse) Array.Reverse(releases);
        using var handler=new Handler((_,_)=>Task.FromResult(Json(SongWithReleases(releases))));
        using var client=new HttpClient(handler); using var gate=new Infrastructure.Providers.MusicBrainz.MusicBrainzRequestGate(TimeProvider.System);
        var song=await Provider(client,gate).GetDetailsAsync(Id,"song",Ct);
        Assert.Equal(earlierId,song!.Metadata.GetProperty("release").GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("null")] [InlineData("[]")] [InlineData("{}")] [InlineData("[null,42,{}]")]
    [InlineData("[{\"id\":\"https://evil.test\",\"title\":\"Bad release\"}]")]
    [InlineData("[{\"id\":\"00000000-0000-0000-0000-000000000000\"}]")]
    public async Task Missing_or_malformed_optional_releases_keep_song_metadata_and_no_artwork(string releases)
    {
        var row=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(Recording())!;
        row["releases"]=JsonSerializer.Deserialize<JsonElement>(releases);
        using var handler=new Handler((_,_)=>Task.FromResult(Json(JsonSerializer.Serialize(row))));
        using var client=new HttpClient(handler); using var gate=new Infrastructure.Providers.MusicBrainz.MusicBrainzRequestGate(TimeProvider.System);
        var song=await Provider(client,gate).GetDetailsAsync(Id,"song",Ct);
        Assert.NotNull(song); Assert.Equal("Survivalism",song.Title); Assert.Null(song.ImageReference);
        Assert.False(song.Metadata.TryGetProperty("release",out _));
    }
}
