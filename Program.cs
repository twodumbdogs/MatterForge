using CMIForge.Data;
using CMIForge.Services;
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
var demoModeEnabled = builder.Configuration.GetValue<bool>("CMIForge:DemoMode");

builder.Services.AddRazorPages(options =>
{
    if (entraOptions.Enabled && !demoModeEnabled)
    {
        options.Conventions.AuthorizeFolder("/");
        options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
        options.Conventions.AllowAnonymousToPage("/Signup");
        options.Conventions.AllowAnonymousToPage("/System/Terms");
        options.Conventions.AllowAnonymousToFolder("/External");
    }

    options.Conventions.ConfigureFilter(new ServiceFilterAttribute(typeof(DemoModePageFilter)));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromHours(4);
});
builder.Services.AddScoped<ProductPlanService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<DemoModeService>();
builder.Services.AddScoped<TenantBrandingService>();
builder.Services.AddScoped<ContentModerationService>();
builder.Services.AddScoped<DemoModePageFilter>();
builder.Services.AddScoped<DemoResetService>();
builder.Services.AddHostedService<DemoResetHostedService>();
builder.Services.AddSingleton<SystemTelemetryService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<DashboardVisibilityService>();
builder.Services.AddScoped<WorkflowNotificationService>();
builder.Services.AddScoped<WorkflowService>();
builder.Services.AddScoped<EmailOutboxDispatcher>();
builder.Services.AddScoped<ExternalFormInviteEmailService>();
builder.Services.AddScoped<ExternalFormInviteTrackingService>();
builder.Services.AddScoped<IEmailSender, GraphEmailSender>();
builder.Services.AddHostedService<EmailOutboxHostedService>();
builder.Services.AddScoped<InboundEmailIntakeService>();
builder.Services.AddScoped<GraphInboundEmailReader>();
builder.Services.AddHostedService<InboundEmailHostedService>();
builder.Services.AddScoped<ConflictSearchArchiveService>();
builder.Services.AddScoped<ConflictSearchService>();
builder.Services.AddScoped<EntityNoteService>();
builder.Services.AddScoped<EntityRelationshipService>();
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<UserDateTimeService>();
builder.Services.AddHttpClient<AddressLookupService>();
builder.Services.AddHttpClient<EntraUserProvisioningService>();
builder.Services.AddHttpClient(nameof(GraphEmailSender));
builder.Services.AddHttpClient(nameof(GraphInboundEmailReader));
builder.Services.Configure<EntraUserProvisioningOptions>(builder.Configuration.GetSection("EntraProvisioning"));
builder.Services.Configure<GraphMailOptions>(builder.Configuration.GetSection("Notifications:Graph"));
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
    builder.Services.AddDbContext<CMIForgeDbContext>(options => options.UseInMemoryDatabase("CMIForgeDev"));
    builder.Services.AddDbContextFactory<CMIForgeDbContext>(
        options => options.UseInMemoryDatabase("CMIForgeDev"),
        ServiceLifetime.Scoped);
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

    builder.Services.AddDbContext<CMIForgeDbContext>(configureDbContext);
    builder.Services.AddDbContextFactory<CMIForgeDbContext>(configureDbContext, ServiceLifetime.Scoped);
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
app.UseSession();
if (entraOptions.Enabled)
{
    app.UseAuthentication();
}
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var telemetry = context.RequestServices.GetRequiredService<SystemTelemetryService>();
    await SystemTelemetryService.TrackRequestAsync(context, next, telemetry);
});

if (entraOptions.Enabled)
{
    app.MapGet("/Account/SignIn", () => Results.Challenge(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" },
        [OpenIdConnectDefaults.AuthenticationScheme]));
    app.MapPost("/Account/SignOut", () => Results.SignOut(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
}

app.MapRazorPages();

app.Run();
