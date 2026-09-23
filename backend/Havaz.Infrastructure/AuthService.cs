using System.Text.RegularExpressions;
using Havaz.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Havaz.Infrastructure;

public sealed class AuthService(HavazDbContext db) : IAuthService
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private readonly PasswordHasher<User> hasher = new();

    public async Task<UserDto?> Register(RegisterRequest request)
    {
        var email = request.Email.Trim();
        var normalized = email.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length > 100) return null;
        if (!EmailPattern.IsMatch(email) || email.Length > 254) return null;
        if (request.Password.Length < 8 || !request.Password.Any(char.IsLetter) || !request.Password.Any(char.IsDigit)) return null;
        if (request.Password != request.PasswordConfirmation) return null;
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized)) return null; // caller returns generic message
        var user = new User { DisplayName = request.DisplayName.Trim(), Email = email, NormalizedEmail = normalized, Role = "USER" };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<UserDto?> Login(string email, string password)
    {
        var normalized = email.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized && u.IsActive);
        if (user is null) return null;
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return ToDto(user);
    }

    public async Task<UserDto?> GetById(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        return user is null || !user.IsActive ? null : ToDto(user);
    }

    private static UserDto ToDto(User u) => new(u.Id, u.DisplayName, u.Email, u.Role, u.CreatedAt);
}
