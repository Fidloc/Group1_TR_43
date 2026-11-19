using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Services.Interfaces;

/// <summary>
/// Сервіс прикладної логіки для сценаріїв керування користувачами (UC-01, UC-02).
/// </summary>
public interface IUserService
{
    Task<(User? User, string? Error)> RegisterAsync(RegisterUserRequest request);
    Task<(User? User, string? Error)> LoginAsync(LoginRequest request);
    Task<UserDto[]> GetAllUsersAsync();
}
