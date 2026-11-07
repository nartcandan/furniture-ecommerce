using FurnitureEcommerce.Application.Categories;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FurnitureEcommerce.Infrastructure.Persistence.Repositories;

public class CategoryRepository(AppDbContext dbContext) : ICategoryRepository
{
    public Task<List<Category>> GetAllAsync(CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Category>> GetActiveAsync(CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);
    }

    public Task<Category?> GetActiveBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(category => category.IsActive && category.Slug == slug, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .AnyAsync(category => category.Slug == slug, cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        dbContext.Categories.Add(category);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException($"A category with slug '{category.Slug}' already exists.");
        }
    }

    public Task UpdateAsync(Category category, CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Category category, CancellationToken cancellationToken)
    {
        dbContext.Categories.Remove(category);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.RestrictViolation or PostgresErrorCodes.ForeignKeyViolation
            })
        {
            throw new ConflictException("A category that contains products cannot be deleted.");
        }
    }
}
