using System.ComponentModel.DataAnnotations;
namespace Marketplace.Application.Accounts;

public record ProfileResult(Guid Id, string FullName, string Email, string? PhoneNumber,
    string? AvatarUrl, string[] Roles, DateTime CreatedAt);
public record UpdateProfileRequest(
    [param: Required, StringLength(255)] string FullName,
    [param: Phone, StringLength(20)] string? PhoneNumber);
public record ChangePasswordRequest(
    [param: Required, StringLength(128)] string CurrentPassword,
    [param: Required, StringLength(128, MinimumLength = 8)] string NewPassword);
public record AddAddressRequest(
    [param: Required, StringLength(150)] string RecipientName,
    [param: Required, Phone, StringLength(20)] string Phone,
    [param: Required, StringLength(100)] string Province,
    [param: Required, StringLength(100)] string District,
    [param: Required, StringLength(100)] string Ward,
    [param: Required, StringLength(190)] string DetailAddress,
    bool IsDefault);
public record AddressResult(Guid Id, string RecipientName, string Phone, string Province,
    string District, string Ward, string DetailAddress, bool IsDefault)
{
    public string FullAddress => string.Join(", ", DetailAddress, Ward, District, Province);
}
