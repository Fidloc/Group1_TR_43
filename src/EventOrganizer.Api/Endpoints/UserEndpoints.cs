using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Services.Interfaces;

namespace EventOrganizer.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users");

        // Реєстрація (UC-01)
        group.MapPost("/register", async (RegisterUserRequest request, IUserService userService) =>
        {
            var (created, error) = await userService.RegisterAsync(request);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Created($"/api/users/{created!.Id}", created);
        });

        // Вхід у систему (UC-02)
        group.MapPost("/login", async (LoginRequest request, IUserService userService) =>
        {
            var (user, error) = await userService.LoginAsync(request);
            if (error is not null)
                return ApiErrors.Result(error);

            return Results.Ok(new { message = "Вхід успішний", userId = user!.Id });
        });

        // Список користувачів (без паролів)
        group.MapGet("/", async (IUserService userService) =>
        {
            var users = await userService.GetAllUsersAsync();
            return Results.Ok(users);
        });

        return group;
    }
}
