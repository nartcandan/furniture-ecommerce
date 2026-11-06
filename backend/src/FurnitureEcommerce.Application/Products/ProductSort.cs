namespace FurnitureEcommerce.Application.Products;

public static class ProductSort
{
    public const string Newest = "newest";
    public const string PriceAscending = "price_asc";
    public const string PriceDescending = "price_desc";
    public const string Name = "name";

    public static readonly string[] All = [Newest, PriceAscending, PriceDescending, Name];
}
