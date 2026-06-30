using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using CoffeeTracker.Api;
using CoffeeTracker.Application;
using CoffeeTracker.Infrastructure;
using CoffeeTracker.Infrastructure.Identity;
using CoffeeTracker.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging: console (captured by `docker logs`) + a rolling file in the persistent volume so
// crashes can be diagnosed post-mortem after the container is recreated. The directory is
// config-driven (FileLog:Directory) — relative `logs/` in dev, `/config/logs` in the container
// (set via FileLog__Directory in the Dockerfile), mirroring how the DB/photos paths are wired.
// The file sink is unbuffered (Serilog's default), so each event is flushed to the OS as it is
// written — a crash won't lose the lines that led up to it.
builder.Host.UseSerilog((context, services, lc) =>
{
    var logDir = context.Configuration["FileLog:Directory"] ?? "logs";
    lc.ReadFrom.Configuration(context.Configuration)   // MinimumLevel + Override (EF SQL silenced)
      .ReadFrom.Services(services)
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .WriteTo.File(
          Path.Combine(logDir, "coffee-.log"),
          rollingInterval: RollingInterval.Day,
          rollOnFileSizeLimit: true,
          fileSizeLimitBytes: 10 * 1024 * 1024,   // 10 MB per file
          retainedFileCountLimit: 5,              // keep at most 5 files (~50 MB cap)
          outputTemplate:
              "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
});

// Add services to the container. Enums serialise as their names (not ints) via a
// [JsonConverter] attribute on the enum type itself — that single annotation drives
// both the JSON wire format and the generated OpenAPI schema (a global converter would
// fix the wire format but the OpenAPI generator wouldn't see it). See RoastLevel.
builder.Services.AddControllers();

// Emit RFC7807 application/problem+json for error responses so every client-error
// shape is consistent — the automatic [ApiController] model-validation 400, the
// explicit Problem(...)/ValidationProblem(...) calls in controllers, and unhandled
// exceptions all share one body format.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Composition root: wire the hexagon's ports to their adapters.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Driving-side adapter: expose the authenticated caller's id to the application
// layer (used to stamp CreatedByUserId) without leaking HttpContext into it.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CoffeeTracker.Application.Ports.Driven.ICurrentUser, CoffeeTracker.Api.Auth.HttpContextCurrentUser>();

// Bind the storage options once, from the same section the adapter binds, so the
// request-boundary limit and the photos directory can't drift from the adapter.
var storageOptions = builder.Configuration
    .GetSection(PhotoStorageOptions.SectionName)
    .Get<PhotoStorageOptions>() ?? new PhotoStorageOptions();

if (storageOptions.MaxPhotoBytes <= 0)
{
    throw new InvalidOperationException(
        $"{PhotoStorageOptions.SectionName}:{nameof(PhotoStorageOptions.MaxPhotoBytes)} must be a positive value.");
}

// Refuse oversized uploads at the request boundary (this caps every endpoint, which
// is fine here — all others take small JSON) rather than after buffering the whole
// body; the adapter still enforces the exact cap. Headroom covers multipart framing,
// clamped so an extreme configured cap can't overflow to a negative limit.
const long multipartFramingHeadroom = 64 * 1024;
var maxRequestBytes = storageOptions.MaxPhotoBytes <= long.MaxValue - multipartFramingHeadroom
    ? storageOptions.MaxPhotoBytes + multipartFramingHeadroom
    : long.MaxValue;
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxRequestBytes);

// --- Authentication (JWT bearer) ---
// The signing key must come from config/env and be strong — never a baked-in
// default (a weak/known key lets anyone forge admin tokens). In Development we
// generate a random ephemeral key when none is configured, so there is no secret
// in git and `dotnet run` still works locally; outside Development a missing/weak
// key is a hard startup failure.
var generatedDevKey = false;
var jwtKey = builder.Configuration[$"{JwtOptions.SectionName}:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < JwtOptions.MinKeyBytes)
{
    if (builder.Environment.IsDevelopment())
    {
        jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        // Write it back in-memory so token issuance (TokenService via IOptions)
        // and validation below resolve the same key.
        builder.Configuration[$"{JwtOptions.SectionName}:Key"] = jwtKey;
        generatedDevKey = true;
    }
    else
    {
        throw new InvalidOperationException(
            $"{JwtOptions.SectionName}:Key must be set to a strong secret of at least {JwtOptions.MinKeyBytes} bytes. " +
            "Provide it via the Jwt__Key environment variable (e.g. `openssl rand -base64 48`).");
    }
}

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            // Pin the algorithm so the key can only ever be used for HS256 — closes
            // any algorithm-substitution ambiguity as defense-in-depth.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

// Account mandatory: every endpoint requires an authenticated user unless it
// explicitly opts out with [AllowAnonymous] (the auth endpoints + dev OpenAPI doc).
// A fallback policy enforces this centrally so a future endpoint can't be left
// public by forgetting [Authorize].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    // Admin-only endpoints (e.g. photo cleanup) require the isAdmin claim the token
    // carries for administrators. A reusable policy so every admin endpoint enforces
    // it the same way and non-admins get a 403 before any handler runs.
    options.AddPolicy(AuthorizationPolicies.Admin, policy =>
        policy.RequireClaim(TokenService.AdminClaim, "true"));
});

// Trust the reverse proxy's forwarded client IP/scheme when configured, so the
// rate limiter partitions by the real client rather than the proxy's single IP
// (behind SWAG/Authelia, RemoteIpAddress is otherwise always the proxy). Proxies
// must be listed in ForwardedHeaders:KnownProxies; absent that, headers are
// ignored (secure default).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    var knownProxies = builder.Configuration["ForwardedHeaders:KnownProxies"];
    foreach (var proxy in (knownProxies ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});

// Throttle the auth endpoints against brute-force/credential-stuffing, keyed by
// client IP. Account-level lockout (Identity) is the second layer.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimiterPolicies.Auth, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
            }));
});

// In dev the Angular dev server (ng serve, :4200) is a different origin from the
// API (:5000). The proxy.conf.json route is the primary path (same-origin to the
// browser, no CORS), but allow the cross-origin case too so hitting the API
// directly from :4200 works. Prod serves the SPA same-origin, so CORS stays off.
const string devCorsPolicy = "DevSpa";
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddPolicy(devCorsPolicy, policy =>
            policy.WithOrigins("http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod()));
}

var app = builder.Build();

if (generatedDevKey)
{
    app.Logger.LogWarning(
        "No {Section}:Key configured — generated a random ephemeral signing key for Development. " +
        "Tokens will be invalidated on restart. Set Jwt__Key for a stable key.", JwtOptions.SectionName);
}

// Honour proxy-forwarded client IP/scheme before anything inspects the connection
// (rate limiter, auth). Must run first in the pipeline.
app.UseForwardedHeaders();

// Behind the NAS reverse proxy, TLS is terminated upstream; emit HSTS in prod so
// browsers stick to HTTPS. TLS itself is the proxy's job — no HttpsRedirection here.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Apply pending migrations on startup (single-instance, self-hosted app). A failure here
// (locked/corrupt DB, bad connection string, failed migration) is the most likely crash-loop
// cause; log it through the configured Serilog logger so it lands in the persistent file — not
// just on stderr — then rethrow so the process still exits non-zero for the orchestrator.
try
{
    await app.Services.InitializeDatabaseAsync();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Database initialization failed during startup — aborting.");
    throw;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Serves the OpenAPI document at /openapi/v1.json (anonymous so the Swagger UI
    // can fetch it under the global auth fallback policy) ...
    app.MapOpenApi().AllowAnonymous();
    // ... and a Swagger UI at the app root that reads it.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CoffeeTracker API v1");
        options.RoutePrefix = string.Empty;
    });
}

// Serve uploaded coffee photos read-only at /photos, from the same directory the
// storage adapter writes to (both resolve the bound PhotosPath the same way).
var photosPath = Path.GetFullPath(storageOptions.PhotosPath);
Directory.CreateDirectory(photosPath);

// Ensure .webp is served with the right content type (older default providers omit it).
var photoContentTypes = new FileExtensionContentTypeProvider();
photoContentTypes.Mappings[".webp"] = "image/webp";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(photosPath),
    RequestPath = "/photos",
    ContentTypeProvider = photoContentTypes,
    // These are user-uploaded files: stop browsers from MIME-sniffing a stored
    // file into active content (e.g. a script disguised as an image).
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff",
});

// Serve the built Angular app (same-origin) from wwwroot in production; in dev the
// SPA is served by `ng serve`, and the root path shows Swagger UI instead.
if (!app.Environment.IsDevelopment())
{
    // Rewrite "/" to "/index.html" so the app's entry point is served by the
    // static middleware below (and gets its no-cache header) rather than the
    // SPA fallback, which would bypass OnPrepareResponse.
    app.UseDefaultFiles();
    app.UseStaticFiles(new StaticFileOptions
    {
        // The service worker's control files must always be revalidated, or the
        // browser keeps serving a stale ngsw.json/worker and never learns a new
        // version was deployed — users get stuck on the old cached app. The
        // hashed JS/CSS bundles are safe to cache long-term (their names change
        // every build), so only the control files get no-cache.
        OnPrepareResponse = ctx =>
        {
            var name = ctx.File.Name;
            if (name is "index.html" or "ngsw.json" or "ngsw-worker.js" or "safety-worker.js")
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            }
        },
    });
}

// Dev-only: allow the ng-serve origin (see the policy registration above).
if (app.Environment.IsDevelopment())
{
    app.UseCors(devCorsPolicy);
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SPA fallback: Angular client-side routes (e.g. /coffees/1) resolve to index.html.
// API/photos/openapi are matched first, so only unknown paths fall through.
// AllowAnonymous so the shell (and `/`) load without auth — otherwise the global
// RequireAuthenticatedUser fallback policy gates this endpoint and unauthenticated
// visitors get 401 instead of the app, with no way to reach the login page. API
// endpoints keep their own auth; only the static SPA shell is public.
if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

app.Run();

// Exposed so integration tests can boot the real app via WebApplicationFactory<Program>.
// (Top-level statements compile to an internal Program; this opts it into test access.)
public partial class Program;
