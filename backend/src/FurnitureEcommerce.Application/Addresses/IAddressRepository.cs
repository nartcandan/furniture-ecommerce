using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.Application.Addresses;

public interface IAddressRepository
{
    Task<List<Address>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns null when the address does not exist or belongs to another user.</summary>
    Task<Address?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task AddAsync(Address address, CancellationToken cancellationToken);
    Task UpdateAsync(Address address, CancellationToken cancellationToken);
    Task DeleteAsync(Address address, CancellationToken cancellationToken);
}
