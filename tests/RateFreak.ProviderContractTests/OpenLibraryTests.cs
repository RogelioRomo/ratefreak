using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.MetadataImport;
using RateFreak.Infrastructure.Providers.OpenLibrary;

namespace RateFreak.ProviderContractTests;

public sealed partial class OpenLibraryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Work = """{"key":"/works/OL45804W","type":{"key":"/type/work"},"title":"Fantastic Mr. Fox","description":{"type":"/type/text","value":"A plain-text description."},"first_publish_date":"1970","covers":[-1,12345],"authors":[{"author":{"key":"/authors/OL34184A"},"type":{"key":"/type/author_role"}}]}""";
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    private static HttpResponseMessage Json(string value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private static OpenLibraryMediaProvider Provider(HttpClient client, OpenLibraryRequestGate gate, ProviderMetadataCache cache, TimeProvider? clock = null, bool enabled = true) =>
        new(client, Options.Create(new OpenLibraryOptions { Enabled = enabled, Contact = "maintainer@example.test" }), gate, cache, clock ?? TimeProvider.System, NullLogger<OpenLibraryMediaProvider>.Instance);

    [Fact]
    public async Task Work_details_keep_work_identity_year_precision_positive_cover_and_bounded_author_enrichment()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.Equal("https", request.RequestUri!.Scheme); Assert.Equal("openlibrary.org", request.RequestUri.Host);
            Assert.Contains("RateFreak/0.1", request.Headers.UserAgent.ToString()); Assert.Contains("maintainer@example.test", request.Headers.UserAgent.ToString());
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(request.RequestUri.AbsolutePath == "/works/OL45804W.json" ? Json(Work) : Json("""{"docs":[{"key":"OL34184A","name":"Roald Dahl"}]}"""));
        });
        using var client = new HttpClient(handler); using var gate = new OpenLibraryRequestGate(TimeProvider.System); using var cache = new ProviderMetadataCache(TimeProvider.System);
        var item = await Provider(client, gate, cache).GetDetailsAsync("OL45804W", "book", Ct);
        Assert.NotNull(item); Assert.Equal("openlibrary", item.Provider); Assert.Equal("OL45804W", item.ExternalId); Assert.Equal("book", item.Type);
        Assert.Equal("Fantastic Mr. Fox", item.Title); Assert.Equal("A plain-text description.", item.Description); Assert.Null(item.ReleaseDate);
        Assert.Equal(1970, item.Metadata.GetProperty("firstPublicationYear").GetInt32());
        Assert.Equal("Roald Dahl", item.Metadata.GetProperty("authors")[0].GetProperty("name").GetString());
        Assert.Equal("https://covers.openlibrary.org/b/id/12345-L.jpg?default=false", item.ImageReference);
        Assert.False(item.Metadata.TryGetProperty("isbn", out _));
    }

    [Fact]
    public async Task Search_is_literal_paginated_and_does_not_fetch_authors_per_row()
    {
        var calls = 0;
        using var handler = new Handler((request, _) =>
        {
            calls++; Assert.Equal("/search.json", request.RequestUri!.AbsolutePath);
            var query = Uri.UnescapeDataString(request.RequestUri.Query);
            Assert.Contains("limit=20", query); Assert.Contains("page=2", query); Assert.DoesNotContain("fields=*", query);
            Assert.Contains("q=\"fox\\\" OR title:*\"", query);
            return Task.FromResult(Json("""{"docs":[{"key":"/works/OL45804W","title":"Fantastic Mr. Fox","first_publish_year":1970,"author_key":["OL34184A"],"author_name":["Roald Dahl"],"cover_i":12345}]}"""));
        });
        using var client = new HttpClient(handler); using var gate = new OpenLibraryRequestGate(TimeProvider.System); using var cache = new ProviderMetadataCache(TimeProvider.System);
        var result = await Provider(client, gate, cache).SearchAsync("fox\" OR title:*", "book", 2, Ct);
        var item = Assert.Single(result.Items); Assert.Equal("OL45804W", item.ExternalId); Assert.Null(item.ReleaseDate); Assert.Equal(1970, item.Metadata.GetProperty("firstPublicationYear").GetInt32()); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("OL45804W", true)] [InlineData("OL1W", true)] [InlineData("OL0W", false)]
    [InlineData("OL045804W", false)] [InlineData("OL45804M", false)] [InlineData("9780140328721", false)]
    [InlineData("/works/OL45804W", false)] [InlineData("ol45804w", false)] [InlineData("OL9999999999999999999999W", false)]
    public void Only_canonical_work_ids_are_importable(string id, bool valid) => Assert.Equal(valid, MediaProviderInput.TryIdentity("openlibrary", "book", id, out _));

    [Theory]
    [InlineData(false, null, true)] [InlineData(true, null, false)] [InlineData(true, "maintainer@example.test", true)]
    [InlineData(true, "https://example.test/contact", false)] [InlineData(true, "x@example.test\r\nHeader: x", false)]
    public void Enabled_instances_require_a_header_safe_contact_email(bool enabled, string? contact, bool valid) => Assert.Equal(valid, new OpenLibraryOptions { Enabled = enabled, Contact = contact }.IsValid());

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"key\":\"/works/OL45804W\",\"title\":\"\\uD800\"}")]
    [InlineData("{\"key\":\"/works/OL12W\",\"title\":\"Wrong work\"}")]
    [InlineData("{\"key\":\"/works/OL45804W\",\"title\":\"A\",\"title\":\"B\"}")]
    [InlineData("{\"key\":\"/works/OL45804W\",\"title\":\"A\",\"covers\":[\"https://evil.test/a\"]}")]
    [InlineData("{\"key\":\"/works/OL45804W\",\"title\":\"A\",\"authors\":[{\"author\":{\"key\":\"/authors/../../bad\"}}]}")]
    public async Task Invalid_work_responses_are_sanitized(string body)
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(body))); using var client = new HttpClient(handler); using var gate = new OpenLibraryRequestGate(TimeProvider.System); using var cache = new ProviderMetadataCache(TimeProvider.System);
        Assert.Equal(ProviderFailure.InvalidResponse, (await Assert.ThrowsAsync<MediaProviderException>(() => Provider(client, gate, cache).GetDetailsAsync("OL45804W", "book", Ct))).Failure);
    }

    [Fact]
    public async Task Missing_optional_fields_and_unavailable_author_lookup_still_import_the_work()
    {
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.StartsWith("/works/", StringComparison.Ordinal) ? Json(Work) : Json("{}", HttpStatusCode.ServiceUnavailable)));
        using var client = new HttpClient(handler); using var gate = new OpenLibraryRequestGate(TimeProvider.System); using var cache = new ProviderMetadataCache(TimeProvider.System);
        var item = await Provider(client, gate, cache).GetDetailsAsync("OL45804W", "book", Ct); Assert.NotNull(item);
        Assert.Equal("OL34184A", item.Metadata.GetProperty("authors")[0].GetProperty("id").GetString()); Assert.Equal(System.Text.Json.JsonValueKind.Null, item.Metadata.GetProperty("authors")[0].GetProperty("name").ValueKind);
    }

    [Fact]
    public async Task Disabled_and_unsupported_calls_do_not_reach_transport()
    {
        using var handler = new Handler((_, _) => throw new InvalidOperationException("No HTTP")); using var client = new HttpClient(handler); using var gate = new OpenLibraryRequestGate(TimeProvider.System); using var cache = new ProviderMetadataCache(TimeProvider.System);
        var provider = Provider(client, gate, cache, enabled: false);
        Assert.Equal(ProviderFailure.Disabled, (await Assert.ThrowsAsync<MediaProviderException>(() => provider.GetDetailsAsync("OL45804W", "book", Ct))).Failure);
        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetDetailsAsync("OL45804M", "book", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.SearchAsync("Fox", "movie", 1, Ct));
    }
}
