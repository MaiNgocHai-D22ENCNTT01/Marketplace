using Marketplace.Application.Shopping;
using Marketplace.Domain.Entities;
using Marketplace.Domain.Enums;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AppDbContext db, AuthTokens tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest input, CancellationToken ct)
    {
        var email = input.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == email, ct))
            return Conflict("Email đã được đăng ký.");
        var customer = new User { Email = email, FullName = input.FullName.Trim(), Cart = new Cart() };
        customer.PasswordHash = new PasswordHasher<User>().HashPassword(customer, input.Password);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO roles (name) VALUES ('CUSTOMER') ON CONFLICT (name) DO NOTHING", ct);
        var role = await db.Roles.SingleAsync(r => r.Name == RoleName.CUSTOMER, ct);
        customer.UserRoles.Add(new UserRole { Role = role });
        db.Users.Add(customer);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict("Email đã được đăng ký."); }
        return StatusCode(201, new { userId = customer.Id });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login(LoginRequest input, CancellationToken ct)
    {
        var email = input.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.Status == UserStatus.ACTIVE, ct);
        if (user is null || new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, input.Password)
            == PasswordVerificationResult.Failed) return Unauthorized("Email hoặc mật khẩu không đúng.");
        return tokens.Issue(user.Id, user.UserRoles.Select(r => r.Role.Name.ToString()).ToArray());
    }
}
