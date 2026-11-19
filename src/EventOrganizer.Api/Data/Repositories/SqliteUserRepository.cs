using EventOrganizer.Api.Data.Interfaces;
using EventOrganizer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventOrganizer.Api.Data.Repositories;

/// <summary>
/// Реалізація сховища користувачів у SQLite через EF Core.
/// </summary>
public sealed class SqliteUserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public SqliteUserRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync();
    }

    public async Task<User> AddUserAsync(User user)
    {
        user.Id = 0; // Дозволяємо SQLite призначити автоінкрементний первинний ключ
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}
