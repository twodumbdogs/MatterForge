using MatterForge.Data;
using MatterForge.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

var entraOptions = builder.Configuration
    .GetSection("Authentication:Microsoft")
    .Get<EntraAuthenticationOptions>() ?? new EntraAuthenticationOptions();
var demoModeEnabled = builder.Configuration.GetValue<bool>("MatterForge:DemoMode");

builder.Services.AddRazorPages(options =>
{
    if (entraOptions.Enabled && !demoModeEnabled)
    {
        options.Conventions.AuthorizeFolder("/");
        options.Conventions.AllowAnonymousToPage("/Signup");
    }

    options.Conventions.ConfigureFilter(new ServiceFilterAttribute(typeof(DemoModePageFilter)));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ProductPlanService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<DemoModeService>();
builder.Services.AddScoped<ContentModerationService>();
builder.Services.AddScoped<DemoModePageFilter>();
builder.Services.AddScoped<DemoResetService>();
builder.Services.AddHostedService<DemoResetHostedService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<ConflictSearchService>();
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<UserDateTimeService>();
builder.Services.AddHttpClient<EntraUserProvisioningService>();
builder.Services.Configure<EntraUserProvisioningOptions>(builder.Configuration.GetSection("EntraProvisioning"));
builder.Services.Configure<SubmissionAttachmentStorageOptions>(builder.Configuration.GetSection("SubmissionAttachments"));
builder.Services.AddScoped<SubmissionAttachmentService>();

builder.Services.Configure<EntraAuthenticationOptions>(builder.Configuration.GetSection("Authentication:Microsoft"));
builder.Services.AddAuthorization();

if (entraOptions.Enabled)
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.Authority = $"https://login.microsoftonline.com/{entraOptions.TenantId}/v2.0";
            options.ClientId = entraOptions.ClientId;
            options.ClientSecret = entraOptions.ClientSecret;
            options.CallbackPath = entraOptions.CallbackPath;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.SaveTokens = true;
            options.Scope.Add("email");
            options.Scope.Add("profile");
        });
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_SERVER", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<MatterForgeDbContext>(options => options.UseInMemoryDatabase("MatterForgeDev"));
    builder.Services.AddDbContextFactory<MatterForgeDbContext>(options => options.UseInMemoryDatabase("MatterForgeDev"));
}
else
{
    Action<DbContextOptionsBuilder> configureDbContext = options =>
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            // Azure SQL serverless can take a beat to wake up after idle.
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
        });

    builder.Services.AddDbContext<MatterForgeDbContext>(configureDbContext);
    builder.Services.AddDbContextFactory<MatterForgeDbContext>(configureDbContext);
}

var app = builder.Build();

await SeedData.EnsureSeededAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.TryAdd("X-Content-Type-Options", "nosniff");
    headers.TryAdd("X-Frame-Options", "DENY");
    headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    headers.TryAdd("Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; worker-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'");

    await next();
});
app.UseStaticFiles();
app.UseRouting();
if (entraOptions.Enabled)
{
    app.UseAuthentication();
}
app.UseAuthorization();

app.MapStaticAssets();
if (entraOptions.Enabled)
{
    app.MapGet("/Account/SignIn", () => Results.Challenge(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" },
        [OpenIdConnectDefaults.AuthenticationScheme]));
    app.MapPost("/Account/SignOut", () => Results.SignOut(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
}

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
