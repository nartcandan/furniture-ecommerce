using System.Linq.Expressions;

namespace FurnitureEcommerce.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CategoryId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public decimal? WidthCm { get; set; }
    public decimal? DepthCm { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = [];

    // The single definition of "can be sold": the product and its category are both active.
    // The public catalog, the cart and order creation all use this rule.
    // The filter is for database queries, the property is for a loaded product with its category.
    public static Expression<Func<Product, bool>> SellableFilter =>
        product => product.IsActive && product.Category.IsActive;

    public bool IsSellable => IsActive && Category.IsActive;
}
