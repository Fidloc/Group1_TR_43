using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Services.Interfaces;

public interface IUserService
{
    Task<(User? User, string? Error)> RegisterAsync(RegisterUserRequest request);
    Task<(User? User, string? Error)> LoginAsync(LoginRequest request);
    Task<UserDto[]> GetAllUsersAsync();
}
