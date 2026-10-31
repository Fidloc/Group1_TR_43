namespace EventOrganizer.Api.Models;

public sealed class User
{
    public int Id { get; init; }
    public string Email { get; init; } = "";
    public string PasswordHash { get; init; } = "";
    public string Name { get; init; } = "";
}
