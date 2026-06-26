namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>
/// Thrown by <see cref="IReviewRepository.AddAsync"/> when persisting a review
/// would violate the one-review-per-user-per-coffee uniqueness rule. Lets the
/// application layer turn the storage-level race backstop into a typed
/// "already reviewed" outcome without depending on any persistence specifics.
/// </summary>
public sealed class DuplicateReviewException : Exception
{
    public DuplicateReviewException()
        : base("A review by this user for this coffee already exists.")
    {
    }

    public DuplicateReviewException(Exception innerException)
        : base("A review by this user for this coffee already exists.", innerException)
    {
    }
}
