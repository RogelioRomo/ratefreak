using System.Reflection;
namespace RateFreak.Infrastructure.Updates;
public sealed record BuildIdentity(string Version, string Revision, bool Official)
{
    public static BuildIdentity Embedded()
    {
        var metadata=typeof(BuildIdentity).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(x=>x.Key,x=>x.Value);
        return new(metadata.GetValueOrDefault("RateFreakVersion")??"unknown",metadata.GetValueOrDefault("RateFreakRevision")??"unknown",
            metadata.GetValueOrDefault("RateFreakOfficial")=="true");
    }
}
