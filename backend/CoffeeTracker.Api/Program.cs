using CoffeeTracker.Application;
using CoffeeTracker.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Composition root: wire the hexagon's ports to their adapters.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Refuse oversized uploads at the request boundary rather than after buffering the
// whole body. The storage adapter still enforces the exact MaxPhotoBytes cap; this
// just bounds what the server will read in. Headroom covers multipart framing.
var maxPhotoBytes = builder.Configuration.GetValue<long?>("Storage:MaxPhotoBytes") ?? 5 * 1024 * 1024;
var maxRequestBytes = maxPhotoBytes + 64 * 1024;
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxRequestBytes);

var app = builder.Build();

// Apply pending migrations on startup (single-instance, self-hosted app).
await app.Services.InitializeDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Serves the OpenAPI document at /openapi/v1.json ...
    app.MapOpenApi();
    // ... and a Swagger UI at the app root that reads it.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CoffeeTracker API v1");
        options.RoutePrefix = string.Empty;
    });
}

// Serve uploaded coffee photos read-only at /photos. Resolved the same way the
// storage adapter resolves Storage:PhotosPath, so writes and reads agree.
var photosPath = Path.GetFullPath(
    builder.Configuration.GetValue<string>("Storage:PhotosPath") ?? "photos");
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

app.UseAuthorization();

app.MapControllers();

app.Run();
