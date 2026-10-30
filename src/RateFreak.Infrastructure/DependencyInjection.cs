using Microsoft.Extensions.DependencyInjection.Extensions;
using RateFreak.Infrastructure.Providers.MusicBrainz;
using RateFreak.Application.Collections;
using RateFreak.Infrastructure.Collections;
using RateFreak.Application.PersonalMedia;
using RateFreak.Infrastructure.PersonalMedia;
using RateFreak.Application.MetadataImport;
using RateFreak.Infrastructure.MetadataImport;
using RateFreak.Infrastructure.Providers.Tmdb;
using RateFreak.Application.Catalog;
using RateFreak.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using RateFreak.Application.Identity;
using RateFreak.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RateFreak.Infrastructure.Persistence;

namespace RateFreak.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("ConnectionStrings"))
            .Validate(options => options.IsValid(),
                "ConnectionStrings:Default must be a valid PostgreSQL connection string with Host, Database and Username.")
            .ValidateOnStart();

        services.AddDbContext<RateFreakDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Default));

        services.AddIdentityCore<AccountUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<RateFreakDbContext>().AddSignInManager();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<RateFreak.Application.Social.IFollowService, RateFreak.Infrastructure.Social.FollowService>();
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ICollectionQueries, CollectionQueries>();
        services.AddScoped<ICollectionCommands, CollectionCommands>();
        services.AddScoped<ICollectionItemCommands, CollectionItemCommands>();
        services.AddScoped<ICollectionFieldCommands, CollectionFieldCommands>();
        services.AddOptions<TmdbOptions>().Bind(configuration.GetSection("Providers:Tmdb"))
            .Validate(options => options.IsValid(), "TMDB configuration is invalid: check timeout, language and enabled-provider access token.")
            .ValidateOnStart();
        services.AddSingleton<TmdbRequestGate>();
        services.AddHttpClient<IMediaProvider, TmdbMediaProvider>("tmdb", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxConnectionsPerServer = 4 })
            .RedactLoggedHeaders(["Authorization"]);
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<MusicBrainzOptions>().Bind(configuration.GetSection("Providers:MusicBrainz"))
            .Validate(options => options.IsValid(), "MusicBrainz configuration is invalid: check timeout and enabled-provider maintainer contact.").ValidateOnStart();
        services.AddSingleton<MusicBrainzRequestGate>();
        services.AddHttpClient<IMediaProvider, MusicBrainzMediaProvider>("musicbrainz", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxConnectionsPerServer = 1 })
            .RedactLoggedHeaders(["User-Agent"]);
        services.AddScoped<IMediaProviderRegistry, MediaProviderRegistry>();
        services.AddScoped<IMediaImportService, MediaImportService>();

        services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("postgresql", tags: ["ready"],
            timeout: TimeSpan.FromSeconds(5));
        return services;
    }
}
