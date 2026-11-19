using Marketplace.Application.Accounts;
using System.ComponentModel.DataAnnotations;
namespace Marketplace.WebClient.Models;
public sealed record AccountProfileModel(ProfileResult Profile, List<AddressResult> Addresses, string Tab);
public sealed class PasswordForm
{
    [Required, StringLength(128)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; set; } = "";
    [Required, Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu mới không khớp.")]
    public string ConfirmPassword { get; set; } = "";
}
