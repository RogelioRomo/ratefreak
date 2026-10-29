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

public sealed class PersonalMediaTests(AccountDatabase database) : IClassFixture<AccountDatabase>
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

    [Fact]
    public async Task Library_query_executes_against_PostgreSQL()
    {
        await using var factory = Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILibraryService>();
        var result = await service.ListAsync(Guid.NewGuid(), new(null, null, 1, 20), Ct);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Library_is_private_and_independent_of_ratings_and_reviews()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory);
        var id = await Media(); var path = "/api/v1/users/me/library/" + id;
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/v1/users/me/library", Ct)).StatusCode);
        await SignIn(owner); await SignIn(other);
        var entry = await Json(await owner.PutAsJsonAsync(path, new { status = "completed",
            startedAt = "2026-01-01", completedAt = "2026-01-02" }, Ct));
        Assert.Equal(id, entry.GetProperty("media").GetProperty("id").GetGuid());
        Assert.Equal("completed", entry.GetProperty("status").GetString());
        Assert.Equal("2026-01-02", entry.GetProperty("completedAt").GetString());
        var list = await owner.GetAsync("/api/v1/users/me/library", Ct);
        Assert.True(list.Headers.CacheControl?.NoStore);
        Assert.Equal(1, (await Json(list)).GetProperty("totalCount").GetInt32());
        Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/v1/users/me/library", Ct)).GetProperty("items").EnumerateArray());
        await Json(await owner.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 4.5 }, Ct));
        await Json(await owner.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "A review", isSpoiler = true }, Ct), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(path, Ct)).StatusCode);
        Assert.Equal(4.5m, (await owner.GetFromJsonAsync<JsonElement>($"/api/v1/media/{id}/rating", Ct)).GetProperty("score").GetDecimal());
        Assert.Equal(1, (await owner.GetFromJsonAsync<JsonElement>($"/api/v1/media/{id}/reviews", Ct)).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Ratings_allow_every_half_step_and_aggregate_only_current_local_votes()
    {
        await using var factory = Factory(); using var first = Client(factory); using var second = Client(factory); using var anonymous = Client(factory);
        var id = await Media(); var rating = $"/api/v1/media/{id}/rating"; var aggregate = rating + "s";
        var empty = await anonymous.GetFromJsonAsync<JsonElement>(aggregate, Ct);
        Assert.Equal(0, empty.GetProperty("count").GetInt32()); Assert.Equal(JsonValueKind.Null, empty.GetProperty("average").ValueKind);
        await SignIn(first); await SignIn(second);
        Assert.Equal(HttpStatusCode.NotFound, (await first.GetAsync(rating, Ct)).StatusCode);
        for (var half = 0; half <= 10; half++)
            Assert.Equal(half / 2m, (await Json(await first.PutAsJsonAsync(rating, new { score = half / 2m }, Ct))).GetProperty("score").GetDecimal());
        await Json(await second.PutAsJsonAsync(rating, new { score = 0m }, Ct));
        var result = await anonymous.GetFromJsonAsync<JsonElement>(aggregate, Ct);
        Assert.Equal(2, result.GetProperty("count").GetInt32()); Assert.Equal(2.5m, result.GetProperty("average").GetDecimal());
        Assert.Empty((await first.GetFromJsonAsync<JsonElement>("/api/v1/users/me/library", Ct)).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NoContent, (await first.DeleteAsync(rating, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await first.DeleteAsync(rating, Ct)).StatusCode);
        result = await anonymous.GetFromJsonAsync<JsonElement>(aggregate, Ct);
        Assert.Equal(1, result.GetProperty("count").GetInt32()); Assert.Equal(0m, result.GetProperty("average").GetDecimal());
        Assert.Equal(HttpStatusCode.NoContent, (await second.DeleteAsync(rating, Ct)).StatusCode);
        result = await anonymous.GetFromJsonAsync<JsonElement>(aggregate, Ct);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("average").ValueKind);
    }

    [Fact]
    public async Task Reviews_are_public_plain_text_and_only_the_author_can_change_them()
    {
        await using var factory = Factory(); using var owner = Client(factory); using var other = Client(factory); using var anonymous = Client(factory);
        var id = await Media(); var userId = await SignIn(owner); await SignIn(other);
        var created = await owner.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "  <b>Literal text</b>\nNext line  ", isSpoiler = true }, Ct);
        var review = await Json(created, HttpStatusCode.Created); var reviewId = review.GetProperty("id").GetGuid();
        var path = "/api/v1/reviews/" + reviewId;
        Assert.Equal(path, created.Headers.Location?.OriginalString);
        Assert.Equal("<b>Literal text</b>\nNext line", review.GetProperty("body").GetString());
        Assert.True(review.GetProperty("isSpoiler").GetBoolean()); Assert.Equal(userId, review.GetProperty("author").GetProperty("id").GetGuid());
        Assert.False(review.GetProperty("author").TryGetProperty("email", out _));
        await Json(await anonymous.GetAsync(path, Ct));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "Duplicate" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync(path, new { body = "Hijacked" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(path, Ct)).StatusCode);
        var edited = await Json(await owner.PatchAsJsonAsync(path, new { body = "Edited", isSpoiler = false }, Ct));
        Assert.Equal(reviewId, edited.GetProperty("id").GetGuid()); Assert.Equal("Edited", edited.GetProperty("body").GetString());
        Assert.False(edited.GetProperty("isSpoiler").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(0, (await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/media/{id}/reviews", Ct)).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("rating", "{}")]
    [InlineData("rating", "{\"score\":null}")]
    [InlineData("rating", "{\"score\":-0.5}")]
    [InlineData("rating", "{\"score\":5.5}")]
    [InlineData("rating", "{\"score\":4.25}")]
    [InlineData("rating", "{\"score\":3,\"userId\":\"x\"}")]
    [InlineData("library", "{\"status\":\"watched\"}")]
    [InlineData("library", "{}")]
    [InlineData("library", "{\"status\":\"completed\",\"startedAt\":\"2026-02-02\",\"completedAt\":\"2026-02-01\"}")]
    [InlineData("library", "{\"status\":\"planned\",\"startedAt\":\"bad\"}")]
    [InlineData("library", "{\"status\":\"planned\",\"userId\":\"x\"}")]
    [InlineData("reviews", "{}")]
    [InlineData("reviews", "{\"body\":\"  \"}")]
    [InlineData("reviews", "{\"body\":\"bad\\u0000text\"}")]
    [InlineData("reviews", "{\"body\":\"bad\\u0001text\"}")]
    [InlineData("reviews", "{\"body\":\"\\uD800\"}")]
    [InlineData("reviews", "{\"body\":\"Good\",\"userId\":\"x\"}")]
    public async Task Invalid_inputs_return_400_without_creating_records(string kind, string payload)
    {
        await using var factory = Factory(); using var client = Client(factory); var id = await Media(); await SignIn(client);
        var path = kind == "library" ? "/api/v1/users/me/library/" + id : $"/api/v1/media/{id}/{kind}";
        var response = await client.SendAsync(new HttpRequestMessage(kind == "reviews" ? HttpMethod.Post : HttpMethod.Put, path)
            { Content = new StringContent(payload, Encoding.UTF8, "application/json") }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"/api/v1/media/{id}/ratings", Ct)).GetProperty("count").GetInt32());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me/library", Ct)).GetProperty("items").EnumerateArray());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"/api/v1/media/{id}/reviews", Ct)).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("PUT", "/api/v1/users/me/library/{0}")]
    [InlineData("DELETE", "/api/v1/users/me/library/{0}")]
    [InlineData("PUT", "/api/v1/media/{0}/rating")]
    [InlineData("DELETE", "/api/v1/media/{0}/rating")]
    [InlineData("POST", "/api/v1/media/{0}/reviews")]
    [InlineData("PATCH", "/api/v1/reviews/{0}")]
    [InlineData("DELETE", "/api/v1/reviews/{0}")]
    public async Task Every_mutation_requires_authentication_and_fresh_identity_bound_csrf(string method, string template)
    {
        await using var factory = Factory(); using var client = Client(factory);
        var path = string.Format(template, await Media());
        async Task<HttpResponseMessage> Send() => await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
            { Content = path.Contains("/library/", StringComparison.Ordinal) ? JsonContent.Create(new { status = "planned" })
                : path.EndsWith("/rating", StringComparison.Ordinal) ? JsonContent.Create(new { score = 3 })
                : JsonContent.Create(new { body = "Text" }) }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send()).StatusCode);
        await SignIn(client); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await Send()).StatusCode);
    }

    [Fact]
    public async Task Library_filters_pages_and_replacements_are_stable_and_owner_scoped()
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client);
        var first = await Media("movie", "A"); var second = await Media("tv_show", "B");
        foreach (var id in new[] { first, second }) await Json(await client.PutAsJsonAsync("/api/v1/users/me/library/" + id, new { status = "planned" }, Ct));
        var a = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me/library?pageSize=1", Ct);
        var b = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me/library?pageSize=1&page=2", Ct);
        Assert.Equal(2, a.GetProperty("totalCount").GetInt32());
        Assert.NotEqual(a.GetProperty("items")[0].GetProperty("media").GetProperty("id").GetGuid(), b.GetProperty("items")[0].GetProperty("media").GetProperty("id").GetGuid());
        var filtered = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me/library?type=tv_show&status=planned", Ct);
        Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
        Assert.Equal(second, filtered.GetProperty("items")[0].GetProperty("media").GetProperty("id").GetGuid());
        foreach (var status in new[] { "in_progress", "paused", "dropped", "completed" })
            Assert.Equal(status, (await Json(await client.PutAsJsonAsync("/api/v1/users/me/library/" + first, new { status }, Ct))).GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, (await Json(await client.PutAsJsonAsync("/api/v1/users/me/library/" + first, new { status = "planned" }, Ct))).GetProperty("startedAt").ValueKind);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=10001")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=0")]
    public async Task Both_lists_reject_unbounded_pagination(string query)
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client); var id = await Media();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/users/me/library?" + query, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/media/{id}/reviews?" + query, Ct)).StatusCode);
    }

    [Fact]
    public async Task Unknown_media_returns_404_and_absent_personal_deletes_are_idempotent()
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client); var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/v1/users/me/library/" + id, new { status = "planned" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 5 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "Text" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/media/{id}/ratings", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/media/{id}/reviews", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/users/me/library/" + id, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/media/{id}/rating", Ct)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_personal_upserts_and_duplicate_reviews_do_not_create_duplicates()
    {
        await using var factory = Factory(); using var client = Client(factory); var user = await SignIn(client); var id = await Media();
        var ratings = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 4.5 }, Ct)));
        Assert.All(ratings, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var libraries = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PutAsJsonAsync("/api/v1/users/me/library/" + id, new { status = "planned" }, Ct)));
        Assert.All(libraries, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var reviews = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "Once" }, Ct)));
        Assert.Single(reviews, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(3, reviews.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await using var db = Context();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        await db.Database.OpenConnectionAsync(Ct);
        command.CommandText = "SELECT (SELECT count(*) FROM ratings WHERE user_id = @u AND media_item_id = @m) + (SELECT count(*) FROM user_media WHERE user_id = @u AND media_item_id = @m) + (SELECT count(*) FROM reviews WHERE user_id = @u AND media_item_id = @m)";
        command.Parameters.Add(new NpgsqlParameter("u", user)); command.Parameters.Add(new NpgsqlParameter("m", id));
        Assert.Equal(3L, await command.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task Private_reads_and_writes_have_separate_per_account_rate_limits()
    {
        await using var factory = Factory(); using var first = Client(factory); using var second = Client(factory);
        await SignIn(first); await SignIn(second); var id = await Media();
        for (var i = 0; i < 30; i++) Assert.Equal(HttpStatusCode.OK,
            (await first.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 3 }, Ct)).StatusCode);
        var limited = await first.DeleteAsync($"/api/v1/media/{id}/rating", Ct);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, (await second.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 4 }, Ct)).StatusCode);
        for (var i = 0; i < 60; i++) Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/v1/users/me/library", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await first.GetAsync("/api/v1/users/me/library", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/v1/users/me/library", Ct)).StatusCode);
    }

    [Theory]
    [InlineData("UPDATE ratings SET score = 4.25 WHERE media_item_id = {0}")]
    [InlineData("UPDATE ratings SET score = 5.5 WHERE media_item_id = {0}")]
    [InlineData("UPDATE ratings SET score = -0.5 WHERE media_item_id = {0}")]
    [InlineData("UPDATE user_media SET status = 'unknown' WHERE media_item_id = {0}")]
    [InlineData("UPDATE user_media SET started_at = '2026-02-02', completed_at = '2026-01-01' WHERE media_item_id = {0}")]
    [InlineData("UPDATE reviews SET body = '   ' WHERE media_item_id = {0}")]
    public async Task Database_constraints_reject_invalid_values_even_without_API_validation(string sql)
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client); var id = await Media();
        await Json(await client.PutAsJsonAsync("/api/v1/users/me/library/" + id, new { status = "planned" }, Ct));
        await Json(await client.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 3 }, Ct));
        await Json(await client.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "Text" }, Ct), HttpStatusCode.Created);
        await using var db = Context();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, new object[] { id }, Ct));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Personal_foreign_keys_prevent_orphans_and_cascade_only_when_the_account_is_deleted()
    {
        await using var factory = Factory(); using var client = Client(factory); var userId = await SignIn(client); var id = await Media();
        await Json(await client.PutAsJsonAsync("/api/v1/users/me/library/" + id, new { status = "planned" }, Ct));
        await Json(await client.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 3 }, Ct));
        await Json(await client.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "Text" }, Ct), HttpStatusCode.Created);
        await using var db = Context();
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => db.MediaItems.Where(x => x.Id == id).ExecuteDeleteAsync(Ct));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deletion.SqlState);
        foreach (var sql in new[] {
            "UPDATE user_media SET media_item_id = {1} WHERE media_item_id = {0}",
            "UPDATE ratings SET media_item_id = {1} WHERE media_item_id = {0}",
            "UPDATE reviews SET media_item_id = {1} WHERE media_item_id = {0}" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, new object[] { id, Guid.NewGuid() }, Ct));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }
        foreach (var sql in new[] {
            "INSERT INTO user_media (id,user_id,media_item_id,status,created_at,updated_at) SELECT gen_random_uuid(),user_id,media_item_id,status,created_at,updated_at FROM user_media WHERE media_item_id = {0}",
            "INSERT INTO ratings (id,user_id,media_item_id,score,created_at,updated_at) SELECT gen_random_uuid(),user_id,media_item_id,score,created_at,updated_at FROM ratings WHERE media_item_id = {0}",
            "INSERT INTO reviews (id,user_id,media_item_id,body,is_spoiler,created_at,updated_at) SELECT gen_random_uuid(),user_id,media_item_id,body,is_spoiler,created_at,updated_at FROM reviews WHERE media_item_id = {0}" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, new object[] { id }, Ct));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        }
        Assert.Equal(1, await db.Users.Where(x => x.Id == userId).ExecuteDeleteAsync(Ct));
        Assert.False(await db.LibraryEntries.AnyAsync(x => x.UserId == userId, Ct));
        Assert.False(await db.Ratings.AnyAsync(x => x.UserId == userId, Ct));
        Assert.False(await db.Reviews.AnyAsync(x => x.UserId == userId, Ct));
        Assert.Equal(1, await db.MediaItems.Where(x => x.Id == id).ExecuteDeleteAsync(Ct));
    }

    [Fact]
    public async Task Review_limits_preserve_valid_text_and_reject_oversize_replacements()
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client); var id = await Media();
        var body = new string('a', 9996) + " 😀";
        var created = await Json(await client.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body }, Ct), HttpStatusCode.Created);
        Assert.Equal(body, created.GetProperty("body").GetString());
        var path = "/api/v1/reviews/" + created.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(path, new { body = new string('a', 10001) }, Ct)).StatusCode);
        Assert.Equal(body, (await client.GetFromJsonAsync<JsonElement>(path, Ct)).GetProperty("body").GetString());
    }

    [Theory]
    [InlineData("status=watched")]
    [InlineData("type=episode")]
    public async Task Library_rejects_unknown_filters(string query)
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/users/me/library?" + query, Ct)).StatusCode);
    }

    [Theory]
    [InlineData("rating")]
    [InlineData("RATING/")]
    public async Task Own_rating_responses_are_not_cacheable_for_any_matched_route_casing(string segment)
    {
        await using var factory = Factory(); using var client = Client(factory); await SignIn(client); var id = await Media();
        await Json(await client.PutAsJsonAsync($"/api/v1/media/{id}/rating", new { score = 2 }, Ct));
        var response = await client.GetAsync($"/api/v1/media/{id}/{segment}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Public_reviews_paginate_stably_with_tied_timestamps_without_exposing_email()
    {
        await using var factory = Factory(); using var first = Client(factory); using var second = Client(factory); using var anonymous = Client(factory);
        await SignIn(first); await SignIn(second); var id = await Media();
        var a = await Json(await first.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "First" }, Ct), HttpStatusCode.Created);
        var b = await Json(await second.PostAsJsonAsync($"/api/v1/media/{id}/reviews", new { body = "Second" }, Ct), HttpStatusCode.Created);
        await using var db = Context();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reviews SET created_at = '2026-01-01T00:00:00Z' WHERE media_item_id = {id}", Ct);
        var path = $"/api/v1/media/{id}/reviews?pageSize=1";
        var one = await anonymous.GetFromJsonAsync<JsonElement>(path, Ct); var two = await anonymous.GetFromJsonAsync<JsonElement>(path + "&page=2", Ct);
        var firstId = one.GetProperty("items")[0].GetProperty("id").GetGuid();
        var secondId = two.GetProperty("items")[0].GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, secondId);
        Assert.Contains(firstId, new[] { a.GetProperty("id").GetGuid(), b.GetProperty("id").GetGuid() });
        Assert.Equal(2, one.GetProperty("totalCount").GetInt32());
        Assert.Equal(firstId, (await anonymous.GetFromJsonAsync<JsonElement>(path, Ct)).GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Empty((await anonymous.GetFromJsonAsync<JsonElement>(path + "&page=3", Ct)).GetProperty("items").EnumerateArray());
        Assert.DoesNotContain("email", one.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }


    // Delete on a separate real connection exactly between the write and response SELECT.
    // Before the fix, autocommit allows DELETE to remove the row and the response is 404/500.
    private sealed class DeleteAfterWrite(string connectionString, string match, string table) : DbCommandInterceptor
    {
        public Guid UserId { get; set; }
        public Guid MediaId { get; set; }
        public bool Attempted { get; private set; }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (Attempted || !command.CommandText.Contains(match, StringComparison.Ordinal)) return result;
            Attempted = true;
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var timeout = new NpgsqlCommand("SET lock_timeout = '250ms'", connection);
            await timeout.ExecuteNonQueryAsync(cancellationToken);
            await using var deletion = new NpgsqlCommand($"DELETE FROM {table} WHERE user_id = @u AND media_item_id = @m", connection);
            deletion.Parameters.AddWithValue("u", UserId); deletion.Parameters.AddWithValue("m", MediaId);
            try { await deletion.ExecuteNonQueryAsync(cancellationToken); }
            catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.LockNotAvailable) { }
            return result;
        }
    }

    [Theory]
    [InlineData("library", "INSERT INTO user_media", "user_media")]
    [InlineData("rating", "INSERT INTO ratings", "ratings")]
    [InlineData("review_create", "INSERT INTO reviews", "reviews")]
    [InlineData("review_update", "UPDATE reviews", "reviews")]
    public async Task Concurrent_delete_between_write_and_response_does_not_turn_success_into_404_or_500(string kind, string match, string table)
    {
        var gate = new DeleteAfterWrite(database.ConnectionString, match, table);
        await using var factory = new ApiFactory(connectionString: database.ConnectionString,
            configureServices: services => services.AddDbContext<RateFreakDbContext>(options => options.AddInterceptors(gate)));
        using var client = Client(factory); gate.UserId = await SignIn(client); gate.MediaId = await Media();
        HttpResponseMessage response;
        if (kind == "library") response = await client.PutAsJsonAsync("/api/v1/users/me/library/" + gate.MediaId, new { status = "planned" }, Ct);
        else if (kind == "rating") response = await client.PutAsJsonAsync($"/api/v1/media/{gate.MediaId}/rating", new { score = 4.5 }, Ct);
        else if (kind == "review_create") response = await client.PostAsJsonAsync($"/api/v1/media/{gate.MediaId}/reviews", new { body = "Created" }, Ct);
        else
        {
            var created = await Json(await client.PostAsJsonAsync($"/api/v1/media/{gate.MediaId}/reviews", new { body = "Created" }, Ct), HttpStatusCode.Created);
            response = await client.PatchAsJsonAsync("/api/v1/reviews/" + created.GetProperty("id").GetGuid(), new { body = "Edited" }, Ct);
        }
        Assert.True(gate.Attempted);
        var result = await Json(response, kind == "review_create" ? HttpStatusCode.Created : HttpStatusCode.OK);
        if (kind == "rating") Assert.Equal(4.5m, result.GetProperty("score").GetDecimal());
        if (kind == "review_update") Assert.Equal("Edited", result.GetProperty("body").GetString());
    }

}
