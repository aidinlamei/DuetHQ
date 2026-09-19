using DuetHQ.Modules.Actions;
using DuetHQ.Modules.CheckIn;
using DuetHQ.Modules.Content;
using DuetHQ.Modules.Insights;
using DuetHQ.Modules.Notifications;
using DuetHQ.Modules.Organization;
using DuetHQ.Modules.Personal;
using DuetHQ.Modules.Pilot;
using DuetHQ.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHealthChecks();

// Explicit composition root: every module is wired here, not through bare project references.
builder.Services
    .AddOrganization(builder.Configuration)
    .AddContent(builder.Configuration)
    .AddCheckIn(builder.Configuration)
    .AddPersonal(builder.Configuration)
    .AddInsights(builder.Configuration)
    .AddActions(builder.Configuration)
    .AddNotifications(builder.Configuration)
    .AddPilot(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapHealthChecks("/health");

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(DuetHQ.Web.Client._Imports).Assembly);

app.Run();
