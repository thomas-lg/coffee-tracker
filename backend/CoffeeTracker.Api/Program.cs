using CoffeeTracker.Application;
using CoffeeTracker.Infrastructure;
using CoffeeTracker.Infrastructure.Storage;
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

app.UseAuthorization();

app.MapControllers();

app.Run();
