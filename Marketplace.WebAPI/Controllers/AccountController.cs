using Marketplace.Application.Accounts;
using Marketplace.Domain.Entities;
using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.WebAPI.Controllers;

[ApiController, Authorize, Route("api/account")]
public sealed class AccountController(AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.Identity!.Name!);

    [HttpGet("profile")]
    public async Task<ActionResult<ProfileResult>> Profile(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.UserRoles).ThenInclude(r => r.Role)
            .SingleAsync(u => u.Id == UserId, ct);
        return new ProfileResult(user.Id, user.FullName, user.Email, user.Phone, null,
            user.UserRoles.Select(r => r.Role.Name.ToString()).ToArray(), user.CreatedAt);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest input, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == UserId, ct);
        user.FullName = input.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockedUser(ct);
        var hasher = new PasswordHasher<User>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed)
            return Conflict("Mật khẩu hiện tại không đúng.");
        if (input.CurrentPassword == input.NewPassword)
            return Conflict("Mật khẩu mới phải khác mật khẩu hiện tại.");
        user.PasswordHash = hasher.HashPassword(user, input.NewPassword);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("addresses")]
    public async Task<List<AddressResult>> Addresses(CancellationToken ct) =>
        await db.Addresses.AsNoTracking().Where(a => a.UserId == UserId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id)
            .Select(a => new AddressResult(a.Id, a.RecipientName, a.Phone, a.Province,
                a.District, a.Ward, a.DetailAddress, a.IsDefault)).ToListAsync(ct);

    // Lock the owner row so all address mutations for this user serialize, even for an empty book.
    private Task<User> LockedUser(CancellationToken ct) => db.Users
        .FromSqlInterpolated($"SELECT * FROM users WHERE id = {UserId} FOR UPDATE").SingleAsync(ct);

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress(AddAddressRequest input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockedUser(ct);
        var existing = await db.Addresses.Where(a => a.UserId == UserId).ToListAsync(ct);
        var makeDefault = input.IsDefault || existing.Count == 0;
        if (makeDefault) foreach (var item in existing) item.IsDefault = false;
        var address = new Address { UserId = UserId, RecipientName = input.RecipientName.Trim(),
            Phone = input.Phone.Trim(), Province = input.Province.Trim(), District = input.District.Trim(),
            Ward = input.Ward.Trim(), DetailAddress = input.DetailAddress.Trim(), IsDefault = makeDefault };
        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StatusCode(201, new AddressResult(address.Id, address.RecipientName, address.Phone,
            address.Province, address.District, address.Ward, address.DetailAddress, address.IsDefault));
    }

    [HttpPut("addresses/{id:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockedUser(ct);
        var addresses = await db.Addresses.Where(a => a.UserId == UserId).ToListAsync(ct);
        if (!addresses.Any(a => a.Id == id)) return NotFound();
        foreach (var address in addresses) address.IsDefault = address.Id == id;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpDelete("addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockedUser(ct);
        var addresses = await db.Addresses.Where(a => a.UserId == UserId).OrderBy(a => a.Id).ToListAsync(ct);
        var address = addresses.FirstOrDefault(a => a.Id == id);
        if (address is null) return NotFound();
        db.Addresses.Remove(address);
        if (address.IsDefault && addresses.FirstOrDefault(a => a.Id != id) is { } next) next.IsDefault = true;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }
}
