namespace EventOrganizer.Api.Contracts;

public sealed record RegisterUserRequest(string Email, string Password, string Name);
