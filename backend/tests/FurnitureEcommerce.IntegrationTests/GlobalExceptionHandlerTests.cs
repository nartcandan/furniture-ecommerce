using System.Net.Sockets;
using System.Text.Json;
using FurnitureEcommerce.Api.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace FurnitureEcommerce.IntegrationTests;

/// <summary>
/// The answers for failures the API cannot cause on purpose in a test: a database that cannot be reached,
/// and a client that leaves. The application's own exceptions are covered by the endpoint tests.
/// </summary>
public class GlobalExceptionHandlerTests
{
    private readonly IServiceProvider services = new ServiceCollection()
        .AddLogging()
        .AddProblemDetails()
        .BuildServiceProvider();

    private GlobalExceptionHandler Handler() => new(
        services.GetRequiredService<IProblemDetailsService>(),
        NullLogger<GlobalExceptionHandler>.Instance);

    private DefaultHttpContext NewContext(CancellationToken requestAborted = default) => new()
    {
        RequestServices = services,
        RequestAborted = requestAborted,
        Response = { Body = new MemoryStream() }
    };

    private static string Body(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    /// <summary>What EF Core throws when the connection drops in the middle of a query.</summary>
    private static Exception DroppedConnection() => new InvalidOperationException(
        "An exception has been raised that is likely due to a transient failure.",
        new NpgsqlException(
            "Exception while reading from stream",
            new IOException("Unable to read data from the transport connection.", new SocketException(10053))));

    [Fact]
    public async Task UnreachableDatabase_IsServiceUnavailable_WithWhenToTryAgain()
    {
        var context = NewContext();

        var handled = await Handler().TryHandleAsync(context, DroppedConnection(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("5", context.Response.Headers.RetryAfter.ToString());

        using var body = JsonDocument.Parse(Body(context));
        Assert.Equal(
            "The store cannot reach its database right now. Please try again in a moment.",
            body.RootElement.GetProperty("detail").GetString());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task UnreachableDatabase_DoesNotShowTheInternalError()
    {
        var context = NewContext();

        await Handler().TryHandleAsync(context, DroppedConnection(), CancellationToken.None);

        var body = Body(context);
        Assert.DoesNotContain("transport connection", body);
        Assert.DoesNotContain("transient", body);
    }

    [Fact]
    public async Task DatabaseTimeout_IsServiceUnavailable()
    {
        var context = NewContext();

        var handled = await Handler().TryHandleAsync(
            context,
            new InvalidOperationException("Query failed.", new TimeoutException("The operation has timed out.")),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    // A mistake in a query does not pass by trying again, so it stays an unexpected error.
    [Fact]
    public async Task QueryError_IsLeftToTheDefaultAnswer()
    {
        var context = NewContext();
        var queryError = new PostgresException("relation \"Missing\" does not exist", "ERROR", "ERROR", "42P01");

        var handled = await Handler().TryHandleAsync(context, queryError, CancellationToken.None);

        Assert.False(handled);
        Assert.False(GlobalExceptionHandler.IsDatabaseUnavailable(queryError));
    }

    [Fact]
    public async Task ClientThatLeft_GetsNoAnswerAndIsNotAnError()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = NewContext(aborted.Token);

        var handled = await Handler().TryHandleAsync(
            context, new OperationCanceledException(aborted.Token), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(GlobalExceptionHandler.ClientClosedRequest, context.Response.StatusCode);
        Assert.Equal(string.Empty, Body(context));
    }

    // Cancelled for another reason while the client still waits: something inside gave up, which is a failure.
    [Fact]
    public async Task CancellationWhileTheClientWaits_IsLeftToTheDefaultAnswer()
    {
        var context = NewContext();

        var handled = await Handler().TryHandleAsync(
            context, new OperationCanceledException(), CancellationToken.None);

        Assert.False(handled);
    }
}
