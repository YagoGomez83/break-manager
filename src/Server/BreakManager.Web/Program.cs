using BreakManager.Application;
using BreakManager.Infrastructure;
using BreakManager.Web.Components;
using BreakManager.Web.Hosting;
using Serilog;

Log.Logger = SerilogSetup.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddObservability();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddHealthChecks().AddInfrastructureHealthChecks(HealthEndpoints.ReadyTag);
    builder.Services.AddRazorComponents().AddInteractiveServerComponents();

    var app = builder.Build();

    if (MigrateCommand.IsRequested(args))
    {
        return await MigrateCommand.RunAsync(app).ConfigureAwait(false);
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseSecurityHeaders();
    app.UseSerilogRequestLogging();
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseAntiforgery();

    app.MapHealthEndpoints();
    app.MapStaticAssets();
    app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

    await app.RunAsync().ConfigureAwait(false);
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "El host terminó de forma inesperada");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}
