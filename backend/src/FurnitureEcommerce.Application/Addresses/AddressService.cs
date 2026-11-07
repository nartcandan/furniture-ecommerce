using FluentValidation;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Addresses;

public class AddressService(
    IAddressRepository repository,
    IValidator<AddressRequest> validator) : IAddressService
{
    public async Task<List<AddressResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var addresses = await repository.GetByUserAsync(userId, cancellationToken);
        return addresses.Select(ToResponse).ToList();
    }

    public async Task<AddressResponse> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        return ToResponse(await GetOwnedAsync(userId, id, cancellationToken));
    }

    public async Task<AddressResponse> CreateAsync(
        Guid userId,
        AddressRequest request,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var address = new Address
        {
            UserId = userId,
            Title = request.Title,
            RecipientFirstName = request.RecipientFirstName,
            RecipientLastName = request.RecipientLastName,
            PhoneNumber = request.PhoneNumber,
            City = request.City,
            District = request.District,
            AddressLine = request.AddressLine,
            PostalCode = request.PostalCode
        };

        await repository.AddAsync(address, cancellationToken);
        return ToResponse(address);
    }

    public async Task<AddressResponse> UpdateAsync(
        Guid userId,
        Guid id,
        AddressRequest request,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var address = await GetOwnedAsync(userId, id, cancellationToken);

        address.Title = request.Title;
        address.RecipientFirstName = request.RecipientFirstName;
        address.RecipientLastName = request.RecipientLastName;
        address.PhoneNumber = request.PhoneNumber;
        address.City = request.City;
        address.District = request.District;
        address.AddressLine = request.AddressLine;
        address.PostalCode = request.PostalCode;
        address.UpdatedAt = DateTime.UtcNow;

        await repository.UpdateAsync(address, cancellationToken);
        return ToResponse(address);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        // Orders keep their own copy of the address, so it can be deleted for good.
        var address = await GetOwnedAsync(userId, id, cancellationToken);
        await repository.DeleteAsync(address, cancellationToken);
    }

    // Another user's address gives the same 404 as a missing one, so ids cannot be probed.
    private async Task<Address> GetOwnedAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        return await repository.GetForUserAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException($"Address '{id}' was not found.");
    }

    private static AddressResponse ToResponse(Address address) => new(
        address.Id,
        address.Title,
        address.RecipientFirstName,
        address.RecipientLastName,
        address.PhoneNumber,
        address.City,
        address.District,
        address.AddressLine,
        address.PostalCode);
}
