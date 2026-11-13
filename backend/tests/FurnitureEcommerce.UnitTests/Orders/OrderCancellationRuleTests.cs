using FurnitureEcommerce.Domain.Entities;

namespace FurnitureEcommerce.UnitTests.Orders;

public class OrderCancellationRuleTests
{
    private static Order NewOrder(OrderStatus status, DateTime? paidAt) => new()
    {
        Status = status,
        PaidAt = paidAt,
        RecipientFirstName = "Ayse",
        RecipientLastName = "Yilmaz",
        PhoneNumber = "+905551112233",
        City = "Istanbul",
        District = "Kadikoy",
        AddressLine = "Moda Cd. No: 1"
    };

    private static DateTime Utc(int day, int hour, int minute) =>
        new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);

    // Turkey is three hours ahead of UTC, so its day changes at 21:00 UTC.
    public static TheoryData<DateTime, DateTime, bool> Times => new()
    {
        // Paid at 10:00 in Turkey, checked at 23:59 the same day.
        { Utc(11, 7, 0), Utc(11, 20, 59), true },
        // Paid at 10:00 in Turkey, checked at 00:00 the next day.
        { Utc(11, 7, 0), Utc(11, 21, 0), false },
        // Paid at 23:30 in Turkey, checked 40 minutes later. Same UTC day, but the next day in Turkey.
        { Utc(11, 20, 30), Utc(11, 21, 10), false },
        // Paid at 00:30 in Turkey, checked at 02:00. Different UTC days, but the same day in Turkey.
        { Utc(10, 21, 30), Utc(10, 23, 0), true },
        // Paid at 00:30 in Turkey, checked 23 hours later, still the same day.
        { Utc(10, 21, 30), Utc(11, 20, 30), true }
    };

    [Theory]
    [MemberData(nameof(Times))]
    public void PaidOrder_CanBeCancelled_OnlyOnTheDayItWasPaidInTurkey(DateTime paidAt, DateTime now, bool expected)
    {
        var order = NewOrder(OrderStatus.Paid, paidAt);

        Assert.Equal(expected, order.CanBeCancelled(now));
    }

    [Fact]
    public void OrderWaitingForPayment_CanBeCancelled_AtAnyTime()
    {
        var order = NewOrder(OrderStatus.PendingPayment, paidAt: null);

        Assert.True(order.CanBeCancelled(Utc(11, 10, 0)));
        Assert.True(order.CanBeCancelled(Utc(12, 10, 0)));
    }

    [Theory]
    // The payment page was opened at 10:00 and lives for 30 minutes.
    [InlineData(0, true)]
    [InlineData(29, true)]
    [InlineData(30, false)]
    [InlineData(45, false)]
    public void OrderWaitingForPayment_CanBePaid_UntilItsPaymentPageExpires(int minutesLater, bool expected)
    {
        var order = NewOrder(OrderStatus.PendingPayment, paidAt: null);
        order.CreatedAt = Utc(11, 10, 0);
        order.PaymentPageUrl = "https://pay/token";

        Assert.Equal(expected, order.CanBePaid(Utc(11, 10, minutesLater), TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void OrderWithoutPaymentPage_CannotBePaid()
    {
        var order = NewOrder(OrderStatus.PendingPayment, paidAt: null);
        order.CreatedAt = Utc(11, 10, 0);

        Assert.False(order.CanBePaid(Utc(11, 10, 1), TimeSpan.FromMinutes(30)));
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Cancelled)]
    public void OrderThatIsNoLongerWaiting_CannotBePaid(OrderStatus status)
    {
        var order = NewOrder(status, paidAt: null);
        order.CreatedAt = Utc(11, 10, 0);
        order.PaymentPageUrl = "https://pay/token";

        Assert.False(order.CanBePaid(Utc(11, 10, 1), TimeSpan.FromMinutes(30)));
    }

    [Theory]
    [InlineData(OrderStatus.PaymentFailed)]
    [InlineData(OrderStatus.Cancelling)]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public void ClosedOrShippedOrder_CannotBeCancelled(OrderStatus status)
    {
        var now = Utc(11, 10, 0);
        var order = NewOrder(status, now.AddMinutes(-5));

        Assert.False(order.CanBeCancelled(now));
    }
}
