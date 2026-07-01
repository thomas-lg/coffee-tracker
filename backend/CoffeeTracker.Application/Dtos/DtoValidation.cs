using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

/// <summary>Shared cross-field validation helpers for the coffee DTOs.</summary>
internal static class DtoValidation
{
    // A coffee can't have been bought in the future, and a date before this is almost
    // certainly a typo (e.g. DateOnly.MinValue) rather than a real purchase.
    private static readonly DateOnly MinPlausibleDate = new(2000, 1, 1);

    public static IEnumerable<ValidationResult> ValidateDateBought(DateOnly dateBought, string memberName)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
