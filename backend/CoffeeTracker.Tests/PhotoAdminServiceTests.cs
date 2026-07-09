using CoffeeTracker.Application.Ports.Driven;
using CoffeeTracker.Application.Services;
using CoffeeTracker.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoffeeTracker.Tests;

// Exercises orphan detection and the delete-safety guard against fakes — the same
// hexagon boundary that lets the catalog tests skip EF/filesystem.
public class PhotoAdminServiceTests
{
    private sealed class FakePhotoStorage(params string[] stored) : IPhotoStorage
    {
        private readonly List<string> _stored = [.. stored];
        public List<string> Deleted { get; } = [];

        // Reflects deletes so ListAsync after a delete is consistent.
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(_stored.Where(p => !Deleted.Contains(p)).ToList());

        public Task<bool> DeleteAsync(string relativePath, CancellationToken ct = default)
        {
            var removed = _stored.Contains(relativePath) && !Deleted.Contains(relativePath);
            Deleted.Add(relativePath);
            return Task.FromResult(removed);
        }

        public Task<PhotoStorageResult> SaveAsync(Stream content, string? contentType, long length, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeCoffeeRepo(params string[] usedPaths) : ICoffeeRepository
    {
        public Task<IReadOnlyList<string>> GetUsedPhotoPathsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([.. usedPaths]);

        // Unused by PhotoAdminService:
        public Task<IReadOnlyList<CoffeeWithStats>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<CoffeeWithStats?> GetWithStatsByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Coffee?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ExistsAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Coffee> AddAsync(Coffee coffee, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Coffee coffee, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
    }

    // Returns the relative path unchanged so assertions can compare against stored paths.
    private sealed class FakePhotoUrlSigner : IPhotoUrlSigner
    {
        public string? Sign(string? relativePath) => relativePath;
        public bool Validate(string fileName, string? exp, string? sig) => true;
    }

    private static PhotoAdminService NewService(IPhotoStorage storage, ICoffeeRepository coffees) =>
        new(storage, coffees, new FakePhotoUrlSigner(), NullLogger<PhotoAdminService>.Instance);

    [Fact]
    public async Task ListAsync_marks_referenced_used_and_orphans_unused()
    {
        var service = NewService(
            new FakePhotoStorage("photos/used.jpg", "photos/orphan.jpg"),
            new FakeCoffeeRepo("photos/used.jpg"));

        var list = await service.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.True(list.Single(p => p.Path == "photos/used.jpg").Used);
        Assert.False(list.Single(p => p.Path == "photos/orphan.jpg").Used);
    }

    [Fact]
    public async Task DeleteAsync_deletes_unused_but_skips_a_referenced_path()
    {
        var storage = new FakePhotoStorage("photos/used.jpg", "photos/orphan.jpg");
        var service = NewService(storage, new FakeCoffeeRepo("photos/used.jpg"));

        var result = await service.DeleteAsync(["photos/orphan.jpg", "photos/used.jpg"]);

        Assert.Equal(1, result.Deleted);
        Assert.Equal(1, result.Skipped);
        Assert.Contains("photos/orphan.jpg", storage.Deleted);
        Assert.DoesNotContain("photos/used.jpg", storage.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_skips_paths_that_are_not_stored()
    {
        var storage = new FakePhotoStorage("photos/orphan.jpg");
        var service = NewService(storage, new FakeCoffeeRepo());

        var result = await service.DeleteAsync(["photos/ghost.jpg"]);

        Assert.Equal(0, result.Deleted);
        Assert.Equal(1, result.Skipped);
        Assert.Empty(storage.Deleted);
    }
}
