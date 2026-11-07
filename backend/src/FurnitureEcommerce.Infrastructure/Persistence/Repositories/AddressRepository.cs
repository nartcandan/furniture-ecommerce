using FurnitureEcommerce.Application.Addresses;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurnitureEcommerce.Infrastructure.Persistence.Repositories;

public class AddressRepository(AppDbContext dbContext) : IAddressRepository
{
    public Task<List<Address>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Addresses
            .AsNoTracking()
            .Where(address => address.UserId == userId)
            .OrderBy(address => address.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Address?> GetForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Addresses
            .FirstOrDefaultAsync(address => address.Id == id && address.UserId == userId, cancellationToken);
    }

    public Task AddAsync(Address address, CancellationToken cancellationToken)
    {
        dbContext.Addresses.Add(address);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(Address address, CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteAsync(Address address, CancellationToken cancellationToken)
    {
        dbContext.Addresses.Remove(address);
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
