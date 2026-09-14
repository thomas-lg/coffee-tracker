using CoffeeTracker.Application.Dtos;

namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Which engines this build carries and whether each can run here.
///
/// A driven port because only the infrastructure knows: availability is a binary on
/// PATH and a model file on disk, and the answer differs between a developer's laptop
/// and the production image.
/// </summary>
public interface IOcrEngineCatalogue
{
    IReadOnlyList<ScanEngineOptionDto> Options();
}
