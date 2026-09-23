using Havaz.Application;
using Havaz.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Havaz.Backend.Tests;

public sealed class AuthTests
{
    private static HavazDbContext Db() => new(new DbContextOptionsBuilder<HavazDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static RegisterRequest Reg(string email) => new("کاربر آزمون", email, "Passw0rd!x", "Passw0rd!x");

    [Fact] public async Task RegisterHashesPasswordAndNormalizesEmail()
    {
        await using var db = Db(); var auth = new AuthService(db);
        var user = await auth.Register(Reg("User@Test.com"));
        Assert.NotNull(user);
        var stored = await db.Users.SingleAsync();
        Assert.Equal("USER@TEST.COM", stored.NormalizedEmail);
        Assert.NotEqual("Passw0rd!x", stored.PasswordHash);
        Assert.DoesNotContain("Passw0rd!x", stored.PasswordHash);
    }

    [Fact] public async Task DuplicateEmailRejected() { await using var db = Db(); var a = new AuthService(db); Assert.NotNull(await a.Register(Reg("a@b.com"))); Assert.Null(await a.Register(Reg("A@B.com"))); }
    [Fact] public async Task WeakPasswordRejected() { await using var db = Db(); var a = new AuthService(db); Assert.Null(await a.Register(new("x", "x@y.com", "short", "short"))); Assert.Null(await a.Register(new("x", "x@y.com", "onlyletters", "onlyletters"))); }
    [Fact] public async Task LoginSuccessAndFailure() { await using var db = Db(); var a = new AuthService(db); await a.Register(Reg("l@t.com")); Assert.NotNull(await a.Login("l@t.com", "Passw0rd!x")); Assert.Null(await a.Login("l@t.com", "wrong1pass")); Assert.Null(await a.Login("nobody@t.com", "Passw0rd!x")); }
    [Fact] public async Task MismatchedConfirmationRejected() { await using var db = Db(); var a = new AuthService(db); Assert.Null(await a.Register(new("x", "x@y.com", "Passw0rd!x", "different1"))); }
}
