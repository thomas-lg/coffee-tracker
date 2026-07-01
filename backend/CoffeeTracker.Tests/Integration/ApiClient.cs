using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Tests.Integration;

// HTTP helpers for the integration suite: register/login a user and issue JSON
// requests with an optional bearer token. Named Get/Post/Put/Delete (no *Async
// suffix) so they don't collide with HttpClient's own instance methods, and the
// optional token keeps anonymous-vs-authenticated calls a one-liner.
internal static class ApiClient
{
    public const string DefaultPassword = "Sup3r-Secret!";

    // Registers a user and returns the auth payload (token + IsAdmin flag). Throws
    // if registration didn't succeed, so callers get a clear failure at the setup
    // step rather than a confusing assertion later.
    public static async Task<AuthResponseDto> RegisterAsync(
        this HttpClient client,
        string email,
        string displayName,
        string password = DefaultPassword)
    {
        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterDto(email, password, displayName));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }

    public static Task<HttpResponseMessage> Get(this HttpClient client, string url, string? token = null) =>
        client.SendAsync(Build(HttpMethod.Get, url, body: null, token));

    public static Task<HttpResponseMessage> Post(this HttpClient client, string url, object? body = null, string? token = null) =>
        client.SendAsync(Build(HttpMethod.Post, url, body, token));

    public static Task<HttpResponseMessage> Put(this HttpClient client, string url, object? body = null, string? token = null) =>
        client.SendAsync(Build(HttpMethod.Put, url, body, token));

    public static Task<HttpResponseMessage> Delete(this HttpClient client, string url, string? token = null) =>
        client.SendAsync(Build(HttpMethod.Delete, url, body: null, token));

    // Posts a single file as multipart/form-data under the field name the upload
    // endpoints bind (`file`). Lets the boundary tests drive the real model-binding +
    // storage path. A null contentType omits the part's Content-Type header.
    public static Task<HttpResponseMessage> PostFile(
        this HttpClient client,
        string url,
        byte[] bytes,
        string? contentType,
        string? token = null,
        string fileName = "upload.bin",
        string fieldName = "file")
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        if (contentType is not null)
        {
            part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        }

        form.Add(part, fieldName, fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client.SendAsync(request);
    }

    /// <summary>A byte sequence whose magic number is a valid PNG header (the bytes
    /// the storage adapter sniffs; the rest need not be a real image).</summary>
    public static byte[] FakePng() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03, 0x04];

    private static HttpRequestMessage Build(HttpMethod method, string url, object? body, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            // Serialize by the runtime type so anonymous objects (used to omit a field,
            // e.g. the missing-roastLevel regression) keep their properties.
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}
