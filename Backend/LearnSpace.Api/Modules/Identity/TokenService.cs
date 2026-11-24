using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LearnSpace.Api.Modules.Identity;

public class TokenService(IConfiguration config)
{
    public const int LifetimeMinutes = 60;

    public (string Token, DateTime ExpiresAt) Create(ApplicationUser user, string role)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var expires = DateTime.UtcNow.AddMinutes(LifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
            // Short claim names on purpose: Program.cs reads "sub" and "role" without claim-type mapping.
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["role"] = role,
                ["name"] = user.DisplayName
            }
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
