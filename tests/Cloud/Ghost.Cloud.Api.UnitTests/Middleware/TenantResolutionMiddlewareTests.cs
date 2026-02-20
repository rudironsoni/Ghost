using System.Security.Claims;
using Ghost.Cloud.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace Ghost.Cloud.Api.UnitTests.Middleware;

public sealed class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithTenantHeader_SetsTenantIdAsync()
    {
        Guid expectedTenantId = Guid.NewGuid();
        DefaultHttpContext context = new();
        context.Request.Headers["X-Tenant-Id"] = expectedTenantId.ToString();

        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.GetTenantId().Should().Be(expectedTenantId);
    }

    [Fact]
    public async Task InvokeAsync_WithAuthenticatedTenantClaim_SetsTenantIdAsync()
    {
        Guid expectedTenantId = Guid.NewGuid();
        DefaultHttpContext context = new();
        var claimsIdentity = new ClaimsIdentity(
            new[] { new Claim("tenant_id", expectedTenantId.ToString()) },
            authenticationType: "TestAuth");
        context.User = new ClaimsPrincipal(claimsIdentity);

        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.GetTenantId().Should().Be(expectedTenantId);
    }

    [Fact]
    public async Task InvokeAsync_WithoutTenantContext_DoesNotFallbackToEmptyGuidAsync()
    {
        DefaultHttpContext context = new();
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.TryGetTenantId(out Guid? tenantId).Should().BeFalse();
        tenantId.Should().BeNull();
        context.Invoking(httpContext => httpContext.GetTenantId())
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*TenantId was not resolved*");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyTenantHeader_DoesNotResolveTenantAsync()
    {
        DefaultHttpContext context = new();
        context.Request.Headers["X-Tenant-Id"] = Guid.Empty.ToString();
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.TryGetTenantId(out Guid? tenantId).Should().BeFalse();
        tenantId.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithAuthenticatedEmptyTenantClaim_DoesNotResolveTenantAsync()
    {
        DefaultHttpContext context = new();
        var claimsIdentity = new ClaimsIdentity(
            new[] { new Claim("tenant_id", Guid.Empty.ToString()) },
            authenticationType: "TestAuth");
        context.User = new ClaimsPrincipal(claimsIdentity);
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.TryGetTenantId(out Guid? tenantId).Should().BeFalse();
        tenantId.Should().BeNull();
    }
}
