using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Data.Interfaces;

/// <summary>
/// Контракт сховища для роботи з користувачами (ISP).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetUserByIdAsync(int id);
    Task<User?> GetUserByEmailAsync(string email);
    Task<List<User>> GetAllUsersAsync();
    Task<User> AddUserAsync(User user);
}
