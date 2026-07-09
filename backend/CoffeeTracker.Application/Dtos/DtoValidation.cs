using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>Shared cross-field validation helpers for the coffee DTOs.</summary>
internal static class DtoValidation
{
    // A coffee can't have been bought in the future, and a date before this is almost
    // certainly a typo (e.g. DateOnly.MinValue) rather than a real purchase.
    private static readonly DateOnly MinPlausibleDate = new(2000, 1, 1);

    public static IEnumerable<ValidationResult> ValidateDateBought(
        DateOnly dateBought, string memberName, ValidationContext validationContext)
    {
        var clock = validationContext.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        // Allow one day of slack so a user ahead of UTC entering their local "today"
        // isn't rejected as a future date.
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1);
        if (dateBought > today)
        {
            yield return new ValidationResult("Date bought cannot be in the future.", [memberName]);
        }
        else if (dateBought < MinPlausibleDate)
        {
            yield return new ValidationResult(
                $"Date bought is implausibly early (before {MinPlausibleDate:yyyy-MM-dd}).", [memberName]);
        }
    }
}
