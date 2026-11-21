using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace RateFreak.Infrastructure.Updates;
public sealed partial class SemanticVersion : IComparable<SemanticVersion>
{
    private readonly BigInteger[] core;
    private readonly string[] prerelease;
    public bool IsPrerelease=>prerelease.Length>0;
    private SemanticVersion(Match match)
    {
        core=[BigInteger.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),BigInteger.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture),BigInteger.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture)];
        prerelease=match.Groups[4].Success?match.Groups[4].Value.Split('.'):[];
    }
    public static bool TryParse(string? value,out SemanticVersion? version)
    {
        version=null;
        if(value is null||value.Length>128)return false;
        var match=Pattern().Match(value);
        if(!match.Success)return false;
        if(match.Groups[4].Success&&match.Groups[4].Value.Split('.').Any(p=>p.All(char.IsAsciiDigit)&&p.Length>1&&p[0]=='0'))return false;
        version=new(match);return true;
    }
    public int CompareTo(SemanticVersion? other)
    {
        if(other is null)return 1;
        for(var i=0;i<3;i++){var c=core[i].CompareTo(other.core[i]);if(c!=0)return c;}
        if(prerelease.Length==0||other.prerelease.Length==0)return (prerelease.Length==0).CompareTo(other.prerelease.Length==0);
        for(var i=0;i<Math.Min(prerelease.Length,other.prerelease.Length);i++)
        {
            var a=prerelease[i];var b=other.prerelease[i];var an=a.All(char.IsAsciiDigit);var bn=b.All(char.IsAsciiDigit);
            var c=an&&bn?BigInteger.Parse(a,CultureInfo.InvariantCulture).CompareTo(BigInteger.Parse(b,CultureInfo.InvariantCulture)):
                an!=bn?(an?-1:1):string.CompareOrdinal(a,b);
            if(c!=0)return c;
        }
        return prerelease.Length.CompareTo(other.prerelease.Length);
    }
    [GeneratedRegex(@"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z",RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
