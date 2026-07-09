using CoffeeTracker.Application.Ports.Driven;
using Microsoft.Extensions.Options;

namespace CoffeeTracker.Infrastructure.Identity;

/// <summary>Adapter exposing the bound <see cref="RegistrationOptions"/> as a driven port.</summary>
public sealed class RegistrationPolicy(IOptions<RegistrationOptions> options) : IRegistrationPolicy
{
    public bool Enabled => options.Value.Enabled;
}
