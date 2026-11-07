namespace FurnitureEcommerce.Application.Addresses;

public interface IAddressService
{
    Task<List<AddressResponse>> GetAllAsync(Guid userId, CancellationToken cancellationToken);
    Task<AddressResponse> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);
    Task<AddressResponse> CreateAsync(Guid userId, AddressRequest request, CancellationToken cancellationToken);
    Task<AddressResponse> UpdateAsync(Guid userId, Guid id, AddressRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
