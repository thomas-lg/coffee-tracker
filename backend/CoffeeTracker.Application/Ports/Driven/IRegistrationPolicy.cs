namespace CoffeeTracker.Application.Ports.Driven;

/// <summary>Whether open registration is currently allowed (bound from deploy config).</summary>
public interface IRegistrationPolicy
{
    bool Enabled { get; }
}
