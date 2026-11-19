using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Marketplace.Application.Shopping;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.WebAPI;

public sealed class AuthTokens
{
    public TokenValidationParameters Validation { get; }

    public AuthTokens(string? secret, bool development)
    {
        var bytes = secret is null && development ? RandomNumberGenerator.GetBytes(32)
            : Convert.FromBase64String(secret ?? throw new InvalidOperationException("Configure Jwt:Key (base64)."));
        if (bytes.Length < 32) throw new InvalidOperationException("Jwt:Key requires at least 32 bytes.");
        Validation = new TokenValidationParameters
        {
            ValidIssuer = "Marketplace.WebAPI", ValidAudience = "Marketplace.Clients",
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(bytes),
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero,
            NameClaimType = "userId", RoleClaimType = "roles"
        };
    }

    public LoginResult Issue(Guid id, string[] roles)
    {
        var expires = DateTime.UtcNow.AddHours(2);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Issuer = Validation.ValidIssuer, Audience = Validation.ValidAudience, Expires = expires,
            Claims = new Dictionary<string, object> { ["userId"] = id.ToString(), ["roles"] = roles },
            SigningCredentials = new SigningCredentials(Validation.IssuerSigningKey, SecurityAlgorithms.HmacSha256)
        });
        return new(token, expires, id, roles);
    }
}
