using CoffeeTracker.Application.Dtos;
using CoffeeTracker.Application.Ports.Driving;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoffeesController(ICoffeeCatalogService catalog) : ControllerBase
{
    /// <summary>Returns the full coffee catalog.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CoffeeResponseDto>>> GetCoffees(CancellationToken ct)
    {
        var coffees = await catalog.GetCatalogAsync(ct);
        return Ok(coffees);
    }
}
