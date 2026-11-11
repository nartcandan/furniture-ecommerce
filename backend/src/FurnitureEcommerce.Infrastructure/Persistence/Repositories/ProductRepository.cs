using FurnitureEcommerce.Application.Common;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Products;
using FurnitureEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FurnitureEcommerce.Infrastructure.Persistence.Repositories;

public class ProductRepository(AppDbContext dbContext) : IProductRepository
{
    public Task<PagedResult<Product>> GetActivePagedAsync(
        ProductQuery query,
        CancellationToken cancellationToken)
    {
        return GetPagedAsync(ActiveProducts(), query, cancellationToken);
    }

    public Task<PagedResult<Product>> GetAllPagedAsync(
        ProductQuery query,
        CancellationToken cancellationToken)
    {
        var products = dbContext.Products
            .AsNoTracking()
            .Include(product => product.Category);

        return GetPagedAsync(products, query, cancellationToken);
    }

    private static async Task<PagedResult<Product>> GetPagedAsync(
        IQueryable<Product> products,
        ProductQuery query,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            products = products.Where(product => product.Category.Slug == query.Category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{EscapeLikePattern(query.Search.Trim())}%";
            products = products.Where(product => EF.Functions.ILike(product.Name, pattern, @"\"));
        }

        if (query.MinPrice is { } minPrice)
        {
            products = products.Where(product => product.Price >= minPrice);
        }

        if (query.MaxPrice is { } maxPrice)
        {
            products = products.Where(product => product.Price <= maxPrice);
        }

        var totalCount = await products.CountAsync(cancellationToken);

        // Lists show only the main image, which is the one with the lowest sort order.
        var productsWithMainImage = products
            .Include(product => product.Images.OrderBy(image => image.SortOrder).Take(1));

        var items = await ApplySort(productsWithMainImage, query.Sort)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<Product?> GetActiveBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        return ActiveProducts()
            .Include(product => product.Images)
            .FirstOrDefaultAsync(product => product.Slug == slug, cancellationToken);
    }

    public Task<Product?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return ActiveProducts()
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Products
            .Include(product => product.Category)
            .Include(product => product.Images)
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken)
    {
        return dbContext.Products
            .AnyAsync(product => product.Slug == slug, cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        dbContext.Products.Add(product);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException($"A product with slug '{product.Slug}' already exists.");
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            throw new ConflictException("The selected category no longer exists.");
        }
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            throw new ConflictException("The selected category no longer exists.");
        }
    }

    public Task AddImageAsync(ProductImage image, CancellationToken cancellationToken)
    {
        dbContext.ProductImages.Add(image);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task DeleteImageAsync(ProductImage image, CancellationToken cancellationToken)
    {
        dbContext.ProductImages.Remove(image);
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryAdjustStockAsync(Guid id, int change, CancellationToken cancellationToken)
    {
        var updatedAt = DateTime.UtcNow;

        var affectedRows = await dbContext.Products
            .Where(product => product.Id == id && product.StockQuantity + change >= 0)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(product => product.StockQuantity, product => product.StockQuantity + change)
                    .SetProperty(product => product.UpdatedAt, updatedAt),
                cancellationToken);

        return affectedRows > 0;
    }

    private IQueryable<Product> ActiveProducts()
    {
        return dbContext.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Where(Product.SellableFilter);
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> products, string? sort)
    {
        // Id is the tie-breaker so that pages stay stable when sort values are equal.
        return sort switch
        {
            ProductSort.PriceAscending => products.OrderBy(product => product.Price).ThenBy(product => product.Id),
            ProductSort.PriceDescending => products.OrderByDescending(product => product.Price).ThenBy(product => product.Id),
            ProductSort.Name => products.OrderBy(product => product.Name).ThenBy(product => product.Id),
            _ => products.OrderByDescending(product => product.CreatedAt).ThenBy(product => product.Id)
        };
    }

    private static string EscapeLikePattern(string value)
    {
        return value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
    }

    private static bool IsForeignKeyViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation
        };
    }
}
