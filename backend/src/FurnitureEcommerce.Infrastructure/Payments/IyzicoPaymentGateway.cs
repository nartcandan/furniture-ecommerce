using System.Globalization;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Application.Payments;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Logging;

namespace FurnitureEcommerce.Infrastructure.Payments;

public class IyzicoPaymentGateway(IyzicoOptions iyzicoOptions, ILogger<IyzicoPaymentGateway> logger)
    : IPaymentGateway
{
    private const string SuccessStatus = "success";
    private const string PaymentSucceeded = "SUCCESS";
    private const string PaymentFailed = "FAILURE";

    // "No payment was found for this token": the customer has not finished the payment page yet.
    private const string NoPaymentForTokenErrorCode = "5122";

    // iyzico requires an identity number. The store does not collect one, so this placeholder is sent.
    private const string PlaceholderIdentityNumber = "11111111111";

    // Order items do not keep the product's category, and iyzico only needs some text here.
    private const string BasketCategory = "Furniture";
    private const string Country = "Turkey";

    private readonly Iyzipay.Options options = new()
    {
        ApiKey = iyzicoOptions.ApiKey,
        SecretKey = iyzicoOptions.SecretKey,
        BaseUrl = iyzicoOptions.BaseUrl
    };

    public async Task<CheckoutStart> StartCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var order = request.Order;
        var orderId = order.Id.ToString();
        var total = ToText(order.TotalAmount);

        var address = new Address
        {
            ContactName = $"{order.RecipientFirstName} {order.RecipientLastName}",
            City = order.City,
            Country = Country,
            Description = $"{order.AddressLine} {order.District}",
            ZipCode = order.PostalCode
        };

        var formRequest = new CreateCheckoutFormInitializeRequest
        {
            Locale = Locale.EN.ToString(),
            // Both carry the order id, so the payment can always be traced back to the order.
            ConversationId = orderId,
            BasketId = orderId,
            Price = total,
            PaidPrice = total,
            Currency = Currency.TRY.ToString(),
            PaymentGroup = PaymentGroup.PRODUCT.ToString(),
            CallbackUrl = request.CallbackUrl,
            // No installments: the customer pays the order total in one payment.
            EnabledInstallments = [1],
            Buyer = new Buyer
            {
                Id = request.Buyer.Id.ToString(),
                Name = request.Buyer.FirstName,
                Surname = request.Buyer.LastName,
                Email = request.Buyer.Email,
                IdentityNumber = PlaceholderIdentityNumber,
                GsmNumber = order.PhoneNumber,
                RegistrationAddress = order.AddressLine,
                City = order.City,
                Country = Country,
                ZipCode = order.PostalCode,
                Ip = request.Buyer.IpAddress
            },
            ShippingAddress = address,
            BillingAddress = address,
            BasketItems = order.Items
                .Select(item => new BasketItem
                {
                    Id = item.ProductId.ToString(),
                    Name = item.ProductName,
                    Category1 = BasketCategory,
                    ItemType = BasketItemType.PHYSICAL.ToString(),
                    Price = ToText(item.UnitPrice * item.Quantity)
                })
                .ToList()
        };

        try
        {
            var form = await CheckoutFormInitialize.Create(formRequest, options);

            if (form.Status == SuccessStatus && form.Token is not null && form.PaymentPageUrl is not null)
            {
                return CheckoutStart.Started(form.Token, form.PaymentPageUrl);
            }

            logger.LogError(
                "iyzico did not open a payment page for order {OrderId}: {ErrorCode} {ErrorMessage}",
                order.Id, form.ErrorCode, form.ErrorMessage);

            return CheckoutStart.Failed(DescribeError(form.ErrorCode, form.ErrorMessage));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "iyzico could not be reached to open a payment page for order {OrderId}.", order.Id);
            return CheckoutStart.Failed("The payment provider could not be reached.");
        }
    }

    public async Task<CheckoutResult> GetCheckoutResultAsync(string token, CancellationToken cancellationToken)
    {
        CheckoutForm form;

        try
        {
            form = await CheckoutForm.Retrieve(new RetrieveCheckoutFormRequest { Token = token }, options);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new PaymentGatewayException("The payment provider could not be reached.", exception);
        }

        if (form.Status == SuccessStatus && form.PaymentStatus == PaymentSucceeded && form.PaymentId is not null)
        {
            return CheckoutResult.Paid(
                form.PaymentId,
                decimal.Parse(form.PaidPrice, CultureInfo.InvariantCulture),
                form.BasketId);
        }

        if (form.PaymentStatus == PaymentFailed)
        {
            return CheckoutResult.Failed(DescribeError(form.ErrorCode, form.ErrorMessage));
        }

        if (form.ErrorCode == NoPaymentForTokenErrorCode)
        {
            return CheckoutResult.NotCompleted();
        }

        // Anything else (wrong keys, provider trouble) says nothing about the payment itself,
        // so the order must not be closed because of it.
        throw new PaymentGatewayException(
            $"Unexpected answer for a payment page: {form.Status} {form.PaymentStatus} {form.ErrorCode} {form.ErrorMessage}");
    }

    public async Task<PaymentCancellation> CancelPaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        try
        {
            var cancel = await Cancel.Create(
                new CreateCancelRequest { Locale = Locale.EN.ToString(), PaymentId = paymentId },
                options);

            return cancel.Status == SuccessStatus
                ? new PaymentCancellation(true)
                : new PaymentCancellation(false, DescribeError(cancel.ErrorCode, cancel.ErrorMessage));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new PaymentGatewayException("The payment provider could not be reached.", exception);
        }
    }

    // iyzico expects a dot as the decimal separator, whatever the server's culture is.
    private static string ToText(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

    private static string DescribeError(string? code, string? message) =>
        string.IsNullOrWhiteSpace(message) ? $"Payment provider error {code}." : $"{message} ({code})";
}
