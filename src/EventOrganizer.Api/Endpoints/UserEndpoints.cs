using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users");

        // Реєстрація (UC-01)
        group.MapPost("/register", async (RegisterUserRequest request, IEventOrganizerRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "invalid_request", message = "Email та пароль обов'язкові" });

            var existing = await repo.GetUserByEmailAsync(request.Email);
            if (existing is not null)
                return Results.Conflict(new { error = "email_already_exists", message = "Користувач з таким email вже існує" });

            var newUser = new User
            {
                Email = request.Email,
                PasswordHash = request.Password,
                Name = request.Name
            };

            var created = await repo.AddUserAsync(newUser);
            return Results.Created($"/api/users/{created.Id}", created);
        });

        // Вхід у систему (UC-02)
        group.MapPost("/login", async (LoginRequest request, IEventOrganizerRepository repo) =>
        {
            var user = await repo.GetUserByEmailAsync(request.Email);
            if (user is null || user.PasswordHash != request.Password)
                return Results.Unauthorized(); // 401 invalid_credentials

            return Results.Ok(new { message = "Вхід успішний", userId = user.Id });
        });

        // Список користувачів (без паролів)
        group.MapGet("/", async (IEventOrganizerRepository repo) =>
        {
            var users = await repo.GetAllUsersAsync();
            return Results.Ok(users.Select(u => new UserDto(u.Id, u.Email, u.Name)).ToArray());
        });

        return group;
    }
}
