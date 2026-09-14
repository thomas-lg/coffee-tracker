using System.ComponentModel.DataAnnotations;
using CoffeeTracker.Application.Ports.Driven;

namespace CoffeeTracker.Application.Dtos;

/// <summary>
/// Which engine snap-to-fill uses, and what the alternatives would cost, as an
/// administrator reads it back.
/// </summary>
/// <param name="Engine">The engine in force.</param>
/// <param name="Options">
/// Every engine this build knows, each saying whether it can actually run on this host.
/// The client needs that: an image built without one of them would otherwise let an
/// administrator pick an engine that answers 503 to every scan.
/// </param>
public record ScanSettingsDto(OcrEngine Engine, IReadOnlyList<ScanEngineOptionDto> Options);

/// <param name="Engine">The engine this option selects.</param>
/// <param name="Available">Whether it is installed and usable on this host.</param>
public record ScanEngineOptionDto(OcrEngine Engine, bool Available);

/// <summary>
/// The chosen engine as a request body. Nullable on purpose: on a non-nullable enum a
/// missing field would bind to the first member and silently change the engine, so an
/// omission has to be a 400 instead.
/// </summary>
public record UpdateScanSettingsDto([Required] OcrEngine? Engine);
