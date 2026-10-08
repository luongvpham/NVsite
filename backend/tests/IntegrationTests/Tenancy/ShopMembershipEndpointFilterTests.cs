using Microsoft.AspNetCore.Http;
using Vsite.Api.Tenancy;
using Vsite.Application.Common.Interfaces;
using Vsite.Application.Identity.Interfaces;
using Vsite.Domain.Exceptions;
using Vsite.Domain.Identity;

namespace Vsite.IntegrationTests;

/// <summary>REFACTOR-BE-001 — kiểm Owner chuyển từ handler về <see cref="ShopMembershipEndpointFilter"/>.
/// Gọi thẳng filter (không host, không Docker) để logic quyền chạy được ở mọi máy; test endpoint
/// thật (Docker) ở `ShopEndpointTests`/`LibraryEndpointTests`/`ShopLogoTests`.</summary>
public sealed class ShopMembershipEndpointFilterTests
{
    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid StaffRoleId = Guid.NewGuid();

    [Fact]
    public async Task Non_member_is_denied_with_SHOP_ACCESS_DENIED()
    {
        var (ex, nextCalled, _) = await InvokeAsync(roleId: null, ownerRequired: true);

        Assert.Equal("SHOP_ACCESS_DENIED", ex?.ErrorCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Non_owner_on_owner_only_endpoint_gets_endpoint_error_code_and_handler_is_not_reached()
    {
        var (ex, nextCalled, _) = await InvokeAsync(StaffRoleId, ownerRequired: true);

        Assert.Equal("CUSTOM_OWNER_REQUIRED", ex?.ErrorCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Non_owner_member_passes_members_only_endpoint()
    {
        var (ex, nextCalled, tenant) = await InvokeAsync(StaffRoleId, ownerRequired: false);

        Assert.Null(ex);
        Assert.True(nextCalled);
        Assert.Equal(ShopId, tenant.ShopId);
    }

    [Fact]
    public async Task Owner_passes_owner_only_endpoint_and_tenant_is_set()
    {
        var (ex, nextCalled, tenant) = await InvokeAsync(WellKnownRoles.OwnerId, ownerRequired: true);

        Assert.Null(ex);
        Assert.True(nextCalled);
        Assert.Equal(ShopId, tenant.ShopId);
    }

    private static async Task<(ForbiddenAccessException? Ex, bool NextCalled, TenantContext Tenant)> InvokeAsync(Guid? roleId, bool ownerRequired)
    {
        var tenant = new TenantContext();
        var filter = new ShopMembershipEndpointFilter(tenant, new FakeUser(), new FakeMembership(roleId));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["shopId"] = ShopId.ToString();
        var metadata = ownerRequired
            ? new EndpointMetadataCollection(new ShopOwnerRequirement("CUSTOM_OWNER_REQUIRED", "x"))
            : new EndpointMetadataCollection();
        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));

        var nextCalled = false;
        try
        {
            await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
            {
                nextCalled = true;
                return ValueTask.FromResult<object?>(null);
            });
            return (null, nextCalled, tenant);
        }
        catch (ForbiddenAccessException ex)
        {
            return (ex, nextCalled, tenant);
        }
    }

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string Audience => "vsite-portal";
    }

    private sealed class FakeMembership(Guid? roleId) : IUserShopMembershipService
    {
        public Task<bool> IsActiveMemberAsync(Guid userId, Guid shopId, CancellationToken cancellationToken) =>
            Task.FromResult(roleId is not null);

        public Task<Guid?> FindActiveRoleIdAsync(Guid userId, Guid shopId, CancellationToken cancellationToken) =>
            Task.FromResult(roleId);
    }
}
