using Microsoft.EntityFrameworkCore;

namespace FurnitureEcommerce.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
