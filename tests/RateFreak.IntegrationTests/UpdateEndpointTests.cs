using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
namespace RateFreak.IntegrationTests;
public sealed partial class ModerationTests
{
    [Fact]
    public async Task Updates_require_current_admin_role_and_disabled_checks_never_contact_upstream()
    {
        await using var f=Factory();using var admin=Client(f);using var ordinary=Client(f);using var anonymous=Client(f);
        var adminId=await Login(admin);var userId=await Login(ordinary);const string path="/api/v1/admin/updates";
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(path,Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await ordinary.GetAsync(path,Ct)).StatusCode);
        await Role(userId,"Moderator");Assert.Equal(HttpStatusCode.Forbidden,(await ordinary.GetAsync(path,Ct)).StatusCode);
        await Role(adminId,"Admin");
        var response=await admin.GetAsync(path,Ct);var body=await Json(response);
        Assert.True(response.Headers.CacheControl?.NoStore);Assert.Equal("disabled",body.GetProperty("state").GetString());
        Assert.False(body.GetProperty("enabled").GetBoolean());Assert.Equal(RateFreak.Infrastructure.Updates.BuildIdentity.Embedded().Version,body.GetProperty("installed").GetProperty("version").GetString());
        var check=await admin.PostAsJsonAsync(path+"/check",new{},Ct);Assert.Equal(HttpStatusCode.OK,check.StatusCode);Assert.True(check.Headers.CacheControl?.NoStore);
        await using var db=Db();await db.UserRoles.Where(x=>x.UserId==adminId).ExecuteDeleteAsync(Ct);
        Assert.Equal(HttpStatusCode.Forbidden,(await admin.GetAsync(path,Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await admin.PostAsJsonAsync(path+"/check",new{},Ct)).StatusCode);
    }
    [Fact]
    public async Task Manual_update_checks_require_csrf_and_use_account_rate_budget()
    {
        await using var f=Factory();using var admin=Client(f);await Role(await Login(admin),"Admin");
        const string path="/api/v1/admin/updates/check";
        var csrf=admin.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();admin.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync(path,new{},Ct)).StatusCode);
        admin.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf);
        for(var i=0;i<9;i++)Assert.Equal(HttpStatusCode.OK,(await admin.PostAsJsonAsync(path,new{},Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await admin.PostAsJsonAsync(path,new{},Ct)).StatusCode);
    }
    private sealed class DelayedUpdates:RateFreak.Application.Updates.IUpdateService
    {
        public readonly TaskCompletionSource<bool> Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Continue=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RateFreak.Application.Updates.UpdateStatus> GetAsync(bool refresh,CancellationToken ct)
        {
            Entered.TrySetResult(true);await Continue.Task.WaitAsync(ct);
            return new(new("0.1.0",new string('a',40),true),true,"available","0.2.0",null,null,null,null,false,true);
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Delayed_update_response_rechecks_administrator_role_before_disclosure(bool manual)
    {
        var delayed=new DelayedUpdates();await using var original=Factory();
        await using var f=original.WithWebHostBuilder(builder=>builder.ConfigureServices(services=>
        {
            Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.RemoveAll<RateFreak.Application.Updates.IUpdateService>(services);
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<RateFreak.Application.Updates.IUpdateService>(services,delayed);
        }));
        using var admin=f.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        var id=await Login(admin);await Role(id,"Admin");
        var pending=manual?admin.PostAsJsonAsync("/api/v1/admin/updates/check",new{},Ct):admin.GetAsync("/api/v1/admin/updates",Ct);
        await delayed.Entered.Task.WaitAsync(Ct);
        await using(var db=Db())await db.UserRoles.Where(x=>x.UserId==id).ExecuteDeleteAsync(Ct);
        delayed.Continue.SetResult(true);var response=await pending;
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.True(response.Headers.CacheControl?.NoStore);
    }
}
