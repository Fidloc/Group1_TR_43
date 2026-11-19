using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Data.Interfaces;
using EventOrganizer.Api.Models;
using EventOrganizer.Api.Services.Interfaces;

namespace EventOrganizer.Api.Services.Implementations;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<(User? User, string? Error)> RegisterAsync(RegisterUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return (null, "missing_credentials");

        var existing = await _userRepository.GetUserByEmailAsync(request.Email);
        if (existing is not null)
            return (null, "email_already_exists");

        var newUser = new User
        {
            Email = request.Email,
            PasswordHash = request.Password,
            Name = request.Name
        };

        var created = await _userRepository.AddUserAsync(newUser);
        return (created, null);
    }

    public async Task<(User? User, string? Error)> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user is null || user.PasswordHash != request.Password)
            return (null, "invalid_credentials");

        return (user, null);
    }

    public async Task<UserDto[]> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllUsersAsync();
        return users.Select(u => new UserDto(u.Id, u.Email, u.Name)).ToArray();
    }
}
