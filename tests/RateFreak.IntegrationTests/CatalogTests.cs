using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RateFreak.Domain.Catalog;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.IntegrationTests;

public sealed class CatalogTests(AccountDatabase database) : IClassFixture<AccountDatabase>
{
    [Fact]
    public async Task Empty_catalog_is_public_and_paginated()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/v1/media", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Empty(json.GetProperty("items").EnumerateArray());
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(20, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, json.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=10001")]
    [InlineData("page=2147483648")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=-1")]
    [InlineData("type=episode")]
    [InlineData("type=0")]
    [InlineData("page=nope")]
    [InlineData("q=%00")]
    public async Task Invalid_query_returns_problem_details(string query)
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/api/v1/media?" + query, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("not-a-guid")]
    public async Task Unknown_media_returns_404(string id)
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/media/" + id, TestContext.Current.CancellationToken)).StatusCode);
    }
    [Fact]
    public async Task Search_details_and_pagination_preserve_canonical_identity()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var movie = new MediaItem("movie", "  Planet 100%_\\ Earth  ", "A synopsis", new DateOnly(2020, 2, 29),
            "https://images.example.invalid/poster.jpg", "{\"runtimeMinutes\":120}");
        var tv = new MediaItem("tv_show", "Planet 100%_\\ Earth");
        var other = new MediaItem("movie", "Planet 100xx Earth");
        context.AddRange(movie, tv, other);
        context.AddRange(new MediaExternalId(movie.Id, "TMDB", "movie", "42"),
            new MediaExternalId(tv.Id, "tmdb", "tv", "42"),
            new MediaExternalId(movie.Id, "imdb", "title", "tt0000042"));
        await context.SaveChangesAsync(cancellation);
        try
        {
            await using var factory = new ApiFactory(connectionString: database.ConnectionString);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            var query = "/api/v1/media?q=" + Uri.EscapeDataString("  pLaNeT 100%_\\  ") + "&pageSize=1";
            var first = await client.GetFromJsonAsync<JsonElement>(query, cancellation);
            var second = await client.GetFromJsonAsync<JsonElement>(query + "&page=2", cancellation);
            Assert.Equal(2, first.GetProperty("totalCount").GetInt32());
            var firstId = first.GetProperty("items")[0].GetProperty("id").GetGuid();
            var secondId = second.GetProperty("items")[0].GetProperty("id").GetGuid();
            Assert.NotEqual(firstId, secondId);
            Assert.Contains(firstId, new[] { movie.Id, tv.Id });
            Assert.Contains(secondId, new[] { movie.Id, tv.Id });
            var repeat = await client.GetFromJsonAsync<JsonElement>(query, cancellation);
            Assert.Equal(firstId, repeat.GetProperty("items")[0].GetProperty("id").GetGuid());
            Assert.False(first.GetProperty("items")[0].TryGetProperty("metadata", out _));
            var filtered = await client.GetFromJsonAsync<JsonElement>(query + "&type=tv_show", cancellation);
            Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
            Assert.Equal(tv.Id, filtered.GetProperty("items")[0].GetProperty("id").GetGuid());
            var pastEnd = await client.GetFromJsonAsync<JsonElement>(query + "&page=3", cancellation);
            Assert.Empty(pastEnd.GetProperty("items").EnumerateArray());
            var detail = await client.GetFromJsonAsync<JsonElement>("/api/v1/media/" + movie.Id, cancellation);
            Assert.Equal("movie", detail.GetProperty("type").GetString());
            Assert.Equal("Planet 100%_\\ Earth", detail.GetProperty("title").GetString());
            Assert.Equal("A synopsis", detail.GetProperty("description").GetString());
            Assert.Equal("2020-02-29", detail.GetProperty("releaseDate").GetString());
            Assert.Equal(120, detail.GetProperty("metadata").GetProperty("runtimeMinutes").GetInt32());
            Assert.Equal(2, detail.GetProperty("externalIds").GetArrayLength());
            Assert.Equal("imdb", detail.GetProperty("externalIds")[0].GetProperty("provider").GetString());
            var tvDetail = await client.GetFromJsonAsync<JsonElement>("/api/v1/media/" + tv.Id, cancellation);
            Assert.Equal(JsonValueKind.Null, tvDetail.GetProperty("releaseDate").ValueKind);
            Assert.Empty(tvDetail.GetProperty("metadata").EnumerateObject());
        }
        finally
        {
            await context.MediaExternalIds.ExecuteDeleteAsync(cancellation);
            await context.MediaItems.ExecuteDeleteAsync(cancellation);
        }
    }

    [Fact]
    public async Task External_identity_is_unique_by_namespace_and_foreign_keys_restrict_deletion()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        var movie = new MediaItem("movie", "Identity test");
        context.Add(movie);
        context.Add(new MediaExternalId(movie.Id, "tmdb", "movie", "1"));
        await context.SaveChangesAsync(cancellation);
        try
        {
            context.Add(new MediaExternalId(movie.Id, "tmdb", "movie", "1"));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellation));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
            context.ChangeTracker.Clear();
            var deletion = await Assert.ThrowsAsync<PostgresException>(() => context.MediaItems.ExecuteDeleteAsync(cancellation));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deletion.SqlState);
            context.Add(new MediaExternalId(Guid.NewGuid(), "tmdb", "tv", "2"));
            var orphan = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellation));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(orphan.InnerException).SqlState);
        }
        finally
        {
            context.ChangeTracker.Clear();
            await context.MediaExternalIds.ExecuteDeleteAsync(cancellation);
            await context.MediaItems.ExecuteDeleteAsync(cancellation);
        }
    }

    [Theory]
    [InlineData("UPDATE media_items SET media_type = 'episode'")]
    [InlineData("UPDATE media_items SET title = '  '")]
    [InlineData("UPDATE media_items SET metadata = '[]'::jsonb")]
    public async Task Database_rejects_invalid_canonical_values(string sql)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var context = CreateContext();
        context.Add(new MediaItem("movie", "Constraint test"));
        await context.SaveChangesAsync(cancellation);
        try
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql, cancellation));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        }
        finally { await context.MediaItems.ExecuteDeleteAsync(cancellation); }
    }

    [Fact]
    public async Task Search_has_an_independent_rate_budget_and_long_queries_are_rejected()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/v1/media?q=" + new string('x', 201), cancellation)).StatusCode);
        for (var i = 1; i < 60; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/media", cancellation)).StatusCode);
        var limited = await client.GetAsync("/api/v1/media", cancellation);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/csrf", cancellation)).StatusCode);
    }

    [Fact]
    public async Task Search_trims_padding_before_applying_length_limit()
    {
        await using var factory = new ApiFactory(connectionString: database.ConnectionString);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/media?q=" + Uri.EscapeDataString(" " + new string('x', 200) + " "),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private RateFreakDbContext CreateContext() => new(new DbContextOptionsBuilder<RateFreakDbContext>()
        .UseNpgsql(database.ConnectionString).Options);

}
