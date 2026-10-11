using GoalGetter.Web;
using GoalGetter.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
// Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
const string ApiServiceUri = "https+http://apiservice";

builder.Services.AddHttpClient<WeatherApiClient>(client => client.BaseAddress = new(ApiServiceUri));

// The shared resilience handler has a total timeout that would end a 90-minute event stream.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental in Microsoft.Extensions.Http.Resilience 10.10.
builder.Services.AddHttpClient<MatchApiClient>(client =>
    {
        client.BaseAddress = new(ApiServiceUri);
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
