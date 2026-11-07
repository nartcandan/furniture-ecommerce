namespace FurnitureEcommerce.Application.Addresses;

public record AddressResponse(
    Guid Id,
    string Title,
    string RecipientFirstName,
    string RecipientLastName,
    string PhoneNumber,
    string City,
    string District,
    string AddressLine,
    string? PostalCode);

/// <summary>Used for both create and update, because every field can be changed.</summary>
public record AddressRequest(
    string Title,
    string RecipientFirstName,
    string RecipientLastName,
    string PhoneNumber,
    string City,
    string District,
    string AddressLine,
    string? PostalCode);
