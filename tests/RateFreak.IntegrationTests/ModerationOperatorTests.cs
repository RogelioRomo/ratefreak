using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace RateFreak.IntegrationTests;

public sealed partial class ModerationTests
{
    [Fact]
    public async Task Offline_command_appoints_existing_account_without_starting_http()
    {
        await using var f = Factory(); using var user = Client(f); var id = await Login(user);
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(typeof(Program).Assembly.Location); start.ArgumentList.Add("--appoint-admin"); start.ArgumentList.Add(id.ToString()); start.ArgumentList.Add("--reason"); start.ArgumentList.Add("Test operator appointment");
        start.Environment["ConnectionStrings__Default"] = database.ConnectionString;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(Ct); var error = process.StandardError.ReadToEndAsync(Ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { process.Kill(entireProcessTree: true); Assert.Fail("Operator command did not exit without starting HTTP."); }
        Assert.Equal(0, process.ExitCode); Assert.DoesNotContain("Now listening", await output); Assert.DoesNotContain(database.ConnectionString, (await error) + (await output));
        await using var db = Db(); Assert.Equal("operator", (await db.ModerationEvents.SingleAsync(x => x.TargetId == id, Ct)).ActorKind);
    }
}
