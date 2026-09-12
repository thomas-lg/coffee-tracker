using System.ComponentModel.DataAnnotations;

namespace CoffeeTracker.Application.Dtos;

public record LoginDto(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Password);
