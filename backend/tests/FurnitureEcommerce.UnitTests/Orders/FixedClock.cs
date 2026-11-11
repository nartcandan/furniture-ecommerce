namespace FurnitureEcommerce.UnitTests.Orders;

/// <summary>A clock that tests can set.</summary>
public class FixedClock : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}
