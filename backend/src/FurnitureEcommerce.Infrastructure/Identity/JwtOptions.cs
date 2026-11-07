using System.Text;

namespace FurnitureEcommerce.Infrastructure.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    // HMAC-SHA256 needs a key of at least 256 bits.
    private const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("'Jwt:Issuer' and 'Jwt:Audience' are not configured.");
        }

        if (Encoding.UTF8.GetByteCount(SigningKey) < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"'Jwt:SigningKey' must be at least {MinimumSigningKeyBytes} bytes long.");
        }

        if (ExpiryMinutes <= 0)
        {
            throw new InvalidOperationException("'Jwt:ExpiryMinutes' must be greater than zero.");
        }
    }
}
