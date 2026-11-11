namespace FurnitureEcommerce.Infrastructure.Payments;

public class IyzicoOptions
{
    public const string SectionName = "Iyzico";

    public string ApiKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey) || string.IsNullOrWhiteSpace(SecretKey))
        {
            throw new InvalidOperationException("'Iyzico:ApiKey' and 'Iyzico:SecretKey' are not configured.");
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("'Iyzico:BaseUrl' is not a valid address.");
        }
    }
}
