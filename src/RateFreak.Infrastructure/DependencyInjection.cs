using RateFreak.Infrastructure.Social;
using RateFreak.Application.Social;
using RateFreak.Infrastructure.Providers.Igdb;
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
        services.AddScoped<RateFreak.Application.Moderation.IModerationService, RateFreak.Infrastructure.Moderation.ModerationService>();
        services.AddOptions<RateFreak.Infrastructure.Installation.InstallationOptions>().Bind(configuration.GetSection("Installation"));
        services.AddScoped<RateFreak.Application.Installation.IInstallationService, RateFreak.Infrastructure.Installation.InstallationService>();
        services.AddScoped<LocalAccountRegistration>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();
        services.AddOptions<AccountDataOptions>().Bind(configuration.GetSection("AccountData"));
        services.AddSingleton<AccountErasureJournal>();
        services.AddScoped<AccountDataService>();
        services.AddScoped<IAccountDataService>(p => p.GetRequiredService<AccountDataService>());
        services.AddScoped<IAccountErasureMaintenance, AccountDataMaintenance>();
        services.AddHostedService<AccountDataRetentionWorker>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IExternalAccountService, ExternalAccountService>();
        services.AddScoped<RateFreak.Application.Social.IFollowService, RateFreak.Infrastructure.Social.FollowService>();
        services.AddOptions<RateFreak.Infrastructure.Artwork.ArtworkOptions>().Bind(configuration.GetSection("Artwork"))
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.RootPath) && Path.IsPathFullyQualified(o.RootPath) && !o.RootPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)), "Artwork requires an absolute persistent directory outside wwwroot.").ValidateOnStart();
        services.AddSingleton<RateFreak.Infrastructure.Artwork.ArtworkCleanupService>();
        services.AddHostedService(p => p.GetRequiredService<RateFreak.Infrastructure.Artwork.ArtworkCleanupService>());
        services.AddSingleton<IArtworkStorage, RateFreak.Infrastructure.Artwork.FileArtworkStorage>();
        services.AddSingleton<IArtworkDecoder, RateFreak.Infrastructure.Artwork.ArtworkDecoder>();
        services.AddScoped<IArtworkService, RateFreak.Infrastructure.Artwork.ArtworkService>();
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICatalogItemCommands, CatalogItemCommands>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReviewCommentService, ReviewCommentService>();
        services.AddScoped<ICollectionQueries, CollectionQueries>();
        services.AddScoped<ICollectionCommands, CollectionCommands>();
        services.AddScoped<ICollectionItemCommands, CollectionItemCommands>();
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
        services.AddOptions<IgdbOptions>().Bind(configuration.GetSection("Providers:Igdb"))
            .Validate(options => options.IsValid(), "IGDB configuration is invalid: check timeout and enabled-provider app credentials.").ValidateOnStart();
        services.AddSingleton<IgdbSession>();
        services.AddHttpClient("igdb-auth", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxConnectionsPerServer = 1 })
            .RedactLoggedHeaders(["Authorization", "Client-ID"]);
        services.AddHttpClient("igdb", client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxConnectionsPerServer = 1 })
            .RedactLoggedHeaders(["Authorization", "Client-ID"])
            .AddTypedClient<IMediaProvider>((client, provider) => new IgdbMediaProvider(
                client, provider.GetRequiredService<IHttpClientFactory>().CreateClient("igdb-auth"),
                provider.GetRequiredService<IOptions<IgdbOptions>>(),
                provider.GetRequiredService<IgdbSession>(), provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<IMediaProviderRegistry, MediaProviderRegistry>();
        services.AddScoped<IMediaImportService, MediaImportService>();

        services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("postgresql", tags: ["ready"],
            timeout: TimeSpan.FromSeconds(5));
        return services;
    }
}
