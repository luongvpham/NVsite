using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vsite.Application.Website.Dtos;
using Vsite.Application.Website.Jobs;
using Vsite.Application.Website.Options;
using Vsite.Infrastructure.Persistence;
using Vsite.IntegrationTests.Media;
using static Vsite.IntegrationTests.Website.WebsiteHandlerKit;

namespace Vsite.IntegrationTests.Website;

/// <summary>WEBSITE-008 T2 — site công khai vẫn phục vụ bản current sau prune. CẦN Docker (Postgres + Redis).</summary>
[Collection(MediaApiCollection.Name)]
public sealed class PublicationPrunerSiteTests(MediaApiFactory factory) : PublishedSiteTestBase(factory)
{
    [Fact]
    public async Task Public_site_still_serves_current_publication_after_prune()
    {
        var (owner, shopId, host) = await OwnerWithWebsiteAsync();
        PublicationDto? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await PublishAsync(owner, shopId);
        }

        string cs;
        await using (var scope = _h.Factory.Services.CreateAsyncScope())
        {
            cs = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()!;
        }

        await using (var db = NewDb(cs, shopId))
        {
            var deleted = await new PublicationPruner(db, new TestTenantContext(shopId), Options.Create(new BuilderRetentionOptions { PublicationsToKeep = 2 }))
                .PruneAsync(default);
            Assert.Equal(2, deleted);
        }

        await InvalidatePointerAsync(shopId);
        var response = await GetSiteAsync(host);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pub = (await JsonOf(response)).GetProperty("publication");
        Assert.Equal(last!.Id, pub.GetProperty("id").GetGuid());
        Assert.Equal(4, pub.GetProperty("version").GetInt32());
    }
}
