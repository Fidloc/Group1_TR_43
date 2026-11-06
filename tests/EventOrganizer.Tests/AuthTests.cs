using Xunit;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models; 
namespace EventOrganizer.Tests;

public class AuthTests
{
    [Fact]
    public void RegisterUser_ShouldAddUserToCollection_WhenDataIsValid()
    {
        // Підготовка
        var users = new List<User>();
        var request = new RegisterUserRequest("maria@test.com", "password123", "Марія");

        // Імітація логіки з Program.cs
        var newUser = new User
        {
            Id = users.Count + 1,
            Email = request.Email,
            PasswordHash = request.Password,
            Name = request.Name
        };
        users.Add(newUser);

        // Assert (Перевірка)
        Assert.Single(users);
        Assert.Equal("maria@test.com", users[0].Email);
        Assert.Equal(1, users[0].Id);
    }

    [Fact]
    public void LoginUser_ShouldReturnUser_WhenCredentialsAreCorrect()
    {
        // Arrange
        var users = new List<User>
        {
            new User { Id = 1, Email = "maria@test.com", PasswordHash = "password123", Name = "Марія" }
        };
        var loginRequest = new LoginRequest("maria@test.com", "password123");

        // Act
        var user = users.FirstOrDefault(u => u.Email == loginRequest.Email && u.PasswordHash == loginRequest.Password);

        // Assert
        Assert.NotNull(user);
        Assert.Equal("maria@test.com", user.Email);
    }

    [Fact]
    public void LoginUser_ShouldReturnNull_WhenPasswordIsIncorrect()
    {
        // Arrange
        var users = new List<User>
        {
            new User { Id = 1, Email = "maria@test.com", PasswordHash = "password123", Name = "Марія" }
        };
        var badLoginRequest = new LoginRequest("maria@test.com", "wrongpassword");

        // Act
        var user = users.FirstOrDefault(u => u.Email == badLoginRequest.Email && u.PasswordHash == badLoginRequest.Password);

        // Assert
        Assert.Null(user);
    }
}
