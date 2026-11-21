using Microsoft.Extensions.Options;
using RateFreak.Application.Updates;
namespace RateFreak.Infrastructure.Updates;
public sealed class UpdateService(GitHubReleaseClient client,IOptions<UpdateOptions> options,BuildIdentity identity,TimeProvider clock):IUpdateService
{
    private readonly object gate=new();
    private Task? running;
    private string state="unchecked";
    private string? latest,etag;
    private DateTimeOffset? attempted,succeeded,automaticAt,manualAt;
    public async Task<UpdateStatus> GetAsync(bool refresh,CancellationToken cancellationToken)
    {
        Task? pending;
        lock(gate)
        {
            if(!options.Value.Enabled)return Snapshot();
            if(running is {IsCompleted:false})pending=running;
            else if((manualAt is null||clock.GetUtcNow()>=manualAt)&&(refresh||automaticAt is null||clock.GetUtcNow()>=automaticAt))
            {
                attempted=clock.GetUtcNow();manualAt=attempted+TimeSpan.FromMinutes(5);state="checking";
                pending=running=Task.Run(CheckAsync);
            }
            else pending=null;
        }
        if(pending is not null)await pending.WaitAsync(cancellationToken);
        lock(gate)return Snapshot();
    }
    private async Task CheckAsync()
    {
        ReleaseResult result;
        try{result=await client.FetchAsync(etag);}
        catch(Exception e) when(e is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException)
        {result=new("unavailable");}
        lock(gate)
        {
            var now=clock.GetUtcNow();
            if(result.Kind=="not-modified"&&succeeded is null)result=new("unavailable");
            if(result.Kind=="unavailable")
            {
                state="unavailable";manualAt=now+TimeSpan.FromMinutes(5);
                if(result.RetryAt>manualAt)manualAt=result.RetryAt;
                automaticAt=manualAt;return;
            }
            succeeded=now;automaticAt=now+TimeSpan.FromHours(24);state=result.Kind=="no-release"?"no-release":"success";
            if(result.Kind!="not-modified"){latest=result.Version;etag=result.ETag;}
        }
    }
    private UpdateStatus Snapshot()
    {
        var enabled=options.Value.Enabled;
        var currentState=enabled?state:"disabled";
        if(enabled&&(!identity.Official||!SemanticVersion.TryParse(identity.Version,out var installed)))currentState="development";
        else if(currentState=="success")
        {
            SemanticVersion.TryParse(identity.Version,out installed);SemanticVersion.TryParse(latest,out var available);
            currentState=available!.CompareTo(installed)>0?"available":"current";
        }
        return new(new(identity.Version,identity.Revision,identity.Official),enabled,currentState,latest,
            latest is null?null:GitHubReleaseClient.Repository+"/releases/tag/v"+Uri.EscapeDataString(latest),attempted,succeeded,manualAt,
            state=="unavailable"&&succeeded is not null,enabled&&(manualAt is null||clock.GetUtcNow()>=manualAt)&&running is not {IsCompleted:false});
    }
}
