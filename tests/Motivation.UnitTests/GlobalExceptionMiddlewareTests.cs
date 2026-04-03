using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Motivation.Api.Middleware;
using Motivation.Application.Exceptions;
using Xunit;

namespace Motivation.UnitTests;

public class GlobalExceptionMiddlewareTests
{
    private static GlobalExceptionMiddleware CreateMiddleware(RequestDelegate next) =>
        new GlobalExceptionMiddleware(next, NullLogger<GlobalExceptionMiddleware>.Instance);

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNextAndDoesNotAlterResponse()
    {
        var called = false;
        var middleware = CreateMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_EmailAlreadyInUseException_Returns409Conflict()
    {
        var middleware = CreateMiddleware(_ => throw new EmailAlreadyInUseException("dup@test.com"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticationFailedException_Returns401Unauthorized()
    {
        var middleware = CreateMiddleware(_ => throw new AuthenticationFailedException("bad credentials"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns403Forbidden()
    {
        var middleware = CreateMiddleware(_ => throw new UnauthorizedAccessException("no access"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_Returns404NotFound()
    {
        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("not found"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400BadRequest()
    {
        var middleware = CreateMiddleware(_ => throw new ArgumentException("bad argument"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentNullException_Returns400BadRequest()
    {
        var middleware = CreateMiddleware(_ => throw new ArgumentNullException("param"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500InternalServerError()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("boom"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task InvokeAsync_Exception_ResponseContentTypeIsJson()
    {
        var middleware = CreateMiddleware(_ => throw new Exception("any error"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_EmailAlreadyInUseException_ResponseBodyContainsEmailInMessage()
    {
        var middleware = CreateMiddleware(_ => throw new EmailAlreadyInUseException("dup@test.com"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("message").GetString().Should().Contain("dup@test.com");
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_ResponseBodyHasGenericMessage()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("internal detail"));
        var context = CreateHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body);
        var msg = json.RootElement.GetProperty("message").GetString();
        msg.Should().Be("An unexpected error occurred.");
        msg.Should().NotContain("internal detail");
    }
}
