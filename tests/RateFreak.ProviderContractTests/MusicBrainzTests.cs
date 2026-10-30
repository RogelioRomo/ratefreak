using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.Providers.MusicBrainz;

namespace RateFreak.ProviderContractTests;

public sealed partial class MusicBrainzTests
{
    private const string Id = "3bd76d40-7f0e-36b7-9348-91a33afee20e";
    private const string Artist = "b7ffd2af-418f-4be2-bdd1-22f8b48613da";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Album(string type = "Album", string date = "2007-04-13") => $$$"""{"id":"{{{Id}}}","title":"Year Zero","primary-type":"{{{type}}}","secondary-types":[],"first-release-date":"{{{date}}}","artist-credit":[{"name":"Nine Inch Nails","joinphrase":"","artist":{"id":"{{{Artist}}}","name":"Nine Inch Nails"}}]}""";
    private sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private static HttpResponseMessage Json(string text, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    private static MusicBrainzMediaProvider Provider(HttpClient client, MusicBrainzRequestGate gate, TimeProvider? clock = null, bool enabled = true) =>
        new(client, Options.Create(new MusicBrainzOptions { Enabled = enabled, Contact = "https://example.test/contact" }), gate, clock ?? TimeProvider.System, NullLogger<MusicBrainzMediaProvider>.Instance);

    [Fact]
    public async Task Details_use_release_group_identity_artist_credits_and_core_metadata_only()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("https", request.RequestUri!.Scheme); Assert.Equal("musicbrainz.org", request.RequestUri.Host);
            Assert.Equal("/ws/2/release-group/" + Id, request.RequestUri.AbsolutePath);
            Assert.Contains("artist-credits", request.RequestUri.Query); Assert.Contains("fmt=json", request.RequestUri.Query);
            Assert.Contains("RateFreak/0.1", request.Headers.UserAgent.ToString()); Assert.Contains("https://example.test/contact", request.Headers.UserAgent.ToString());
            Assert.Null(request.Headers.Authorization); return Task.FromResult(Json(Album()));
        });
        using var client = new HttpClient(handler); using var gate = new MusicBrainzRequestGate(TimeProvider.System);
        var item = await Provider(client, gate).GetDetailsAsync(Id.ToUpperInvariant(), "album", Ct);
        Assert.NotNull(item); Assert.Equal(Id, item.ExternalId); Assert.Equal("musicbrainz", item.Provider); Assert.Equal("album", item.Type);
        Assert.Equal("Year Zero", item.Title); Assert.Equal(new DateOnly(2007,4,13), item.ReleaseDate); Assert.Null(item.Description); Assert.Null(item.ImageReference);
        Assert.Equal("Nine Inch Nails", item.Metadata.GetProperty("artistCredit").GetString()); Assert.Equal(Artist, item.Metadata.GetProperty("artists")[0].GetProperty("id").GetString());
        Assert.False(item.Metadata.TryGetProperty("rating", out _));
    }
    [Fact]
    public async Task Search_escapes_literal_input_and_keeps_fixed_album_filter_and_pagination()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("/ws/2/release-group", request.RequestUri!.AbsolutePath);
            var query = Uri.UnescapeDataString(request.RequestUri.Query);
            Assert.Contains("primarytype:album", query); Assert.Contains("offset=20", query); Assert.Contains("limit=20", query);
            Assert.Contains("\\\" OR primarytype:single", query);
            return Task.FromResult(Json("{\"release-groups\":[" + Album() + "," + Album("EP") + "]}"));
        });
        using var client = new HttpClient(handler); using var gate = new MusicBrainzRequestGate(TimeProvider.System);
        var result = await Provider(client, gate).SearchAsync("\" OR primarytype:single", "album", 2, Ct);
        Assert.Equal(2, result.Page); Assert.Single(result.Items);
    }
    [Theory]
    [InlineData("1999", null)] [InlineData("1999-02", null)] [InlineData("2024-02-29", "2024-02-29")] [InlineData("", null)]
    public async Task Partial_dates_preserve_precision_without_inventing_days(string date, string? complete)
    {
        using var handler = new Handler((_,_) => Task.FromResult(Json(Album(date: date)))); using var client = new HttpClient(handler); using var gate = new MusicBrainzRequestGate(TimeProvider.System);
        var item = await Provider(client,gate).GetDetailsAsync(Id,"album",Ct); Assert.NotNull(item);
        Assert.Equal(complete, item.ReleaseDate?.ToString("yyyy-MM-dd"));
        if (date.Length > 0) Assert.Equal(date,item.Metadata.GetProperty("firstReleaseDate").GetString());
    }
    [Theory]
    [InlineData("EP")] [InlineData("Single")] [InlineData("Broadcast")] [InlineData("Other")]
    public async Task Non_album_details_are_not_importable(string type)
    {
        using var handler = new Handler((_,_) => Task.FromResult(Json(Album(type)))); using var client = new HttpClient(handler); using var gate = new MusicBrainzRequestGate(TimeProvider.System);
        Assert.Null(await Provider(client,gate).GetDetailsAsync(Id,"album",Ct));
    }
    [Theory]
    [InlineData("Audiobook")] [InlineData("Audio drama")] [InlineData("Spokenword")] [InlineData("Interview")]
    public async Task Nonmusical_secondary_types_are_excluded(string type)
    {
        using var handler = new Handler((_,_) => Task.FromResult(Json(Album().Replace("\"secondary-types\":[]", "\"secondary-types\":[\""+type+"\"]"))));
        using var client = new HttpClient(handler); using var gate = new MusicBrainzRequestGate(TimeProvider.System);
        Assert.Null(await Provider(client,gate).GetDetailsAsync(Id,"album",Ct));
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("{\"id\":\"bad\",\"title\":\"Title\",\"primary-type\":\"Album\",\"artist-credit\":[]}")]
    [InlineData("{\"id\":\"3bd76d40-7f0e-36b7-9348-91a33afee20e\",\"title\":\"x\",\"title\":\"y\"}")]
    public async Task Invalid_responses_are_sanitized(string body)
    {
        using var handler = new Handler((_,_) => Task.FromResult(Json(body))); using var client = new HttpClient(handler); using var gate = new MusicBrainzRequestGate(TimeProvider.System);
        var error = await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client,gate).GetDetailsAsync(Id,"album",Ct)); Assert.Equal(ProviderFailure.InvalidResponse,error.Failure);
    }
    [Theory]
    [InlineData("2026-02-29")] [InlineData("1999-13")] [InlineData("0000")] [InlineData("99")]
    public async Task Invalid_dates_do_not_reach_storage(string date)
    {
        using var handler = new Handler((_,_) => Task.FromResult(Json(Album(date:date)))); using var client = new HttpClient(handler); using var gate = new MusicBrainzRequestGate(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse,(await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client,gate).GetDetailsAsync(Id,"album",Ct))).Failure);
    }
    [Theory]
    [InlineData(false,null,true)] [InlineData(true,null,false)] [InlineData(true,"bad\r\nheader",false)] [InlineData(true,"https://example.test/contact",true)] [InlineData(true,"maintainer@example.test",true)] [InlineData(true,"http://example.test",false)]
    public void Configuration_requires_safe_contact_when_enabled(bool enabled,string? contact,bool valid) =>
        Assert.Equal(valid,new MusicBrainzOptions { Enabled=enabled,Contact=contact }.IsValid());
    [Fact]
    public async Task Disabled_provider_does_not_call_transport()
    {
        using var handler = new Handler((_,_) => throw new InvalidOperationException("No HTTP")); using var client = new HttpClient(handler); using var gate = new MusicBrainzRequestGate(TimeProvider.System);
        Assert.Equal(ProviderFailure.Disabled,(await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client,gate,enabled:false).GetDetailsAsync(Id,"album",Ct))).Failure);
    }
    [Theory]
    [InlineData("tmdb","album","11",false)] [InlineData("musicbrainz","movie","11",false)] [InlineData("musicbrainz","album",Id,true)] [InlineData("musicbrainz","album","00000000-0000-0000-0000-000000000000",false)] [InlineData("musicbrainz","album","{3bd76d40-7f0e-36b7-9348-91a33afee20e}",false)]
    public void Provider_identities_validate_provider_specific_types_and_ids(string provider,string type,string id,bool valid) =>
        Assert.Equal(valid,MediaProviderInput.TryIdentity(provider,type,id,out _));
    [Fact]
    public async Task Tmdb_rejects_album_before_outbound_access()
    {
        using var handler = new Handler((_,_) => throw new InvalidOperationException("No HTTP")); using var client = new HttpClient(handler); using var gate = new Infrastructure.Providers.Tmdb.TmdbRequestGate();
        var tmdb=new Infrastructure.Providers.Tmdb.TmdbMediaProvider(client,Options.Create(new Infrastructure.Providers.Tmdb.TmdbOptions()),gate,NullLogger<Infrastructure.Providers.Tmdb.TmdbMediaProvider>.Instance);
        await Assert.ThrowsAsync<ArgumentException>(()=>tmdb.GetDetailsAsync("11","album",Ct));
    }
}
