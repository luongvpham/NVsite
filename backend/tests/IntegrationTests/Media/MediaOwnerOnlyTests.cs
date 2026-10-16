using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vsite.Domain.Identity;

namespace Vsite.IntegrationTests.Media;

/// <summary>
/// T10b (WEBSITE-001, #100, R16) — MỌI endpoint Media chỉ Owner (đọc lẫn ghi). Trước đây 8 endpoint chỉ cần membership nên một
/// Customer/Manager/Staff có token global của shop dùng được thư viện ảnh. Đây là ĐỔI HÀNH VI CÓ CHỦ ĐÍCH (xem changelog).
/// CẦN Docker (Testcontainers) — dùng chung <see cref="MediaApiFactory"/>.
/// </summary>
[Collection(MediaApiCollection.Name)]
public sealed class MediaOwnerOnlyTests(MediaApiFactory factory)
{
    private readonly ShopApiHarness _h = new(factory);

    private static MultipartFormDataContent Multipart(bool withPreset)
    {
        var content = new MultipartFormDataContent { { new ByteArrayContent(Imaging.TestImages.Jpeg(800, 600)), "file", "photo.jpg" } };
        if (withPreset)
        {
            content.Add(new StringContent("800x600,cover"), "preset");
        }

        return content;
    }

    /// <summary>(tên operationId, method, đường dẫn, body) — body dựng mới cho mỗi lần gửi.</summary>
    private static IEnumerable<(string Name, HttpMethod Method, string Path, Func<HttpContent?> Body)> AllEndpoints(Guid shopId, Guid assetId) =>
    [
        ("uploadToSlot", HttpMethod.Post, $"/api/shops/{shopId}/media/slot-uploads", () => Multipart(withPreset: true)),
        ("uploadToLibrary", HttpMethod.Post, $"/api/shops/{shopId}/media/library", () => Multipart(withPreset: false)),
        ("listLibrary", HttpMethod.Get, $"/api/shops/{shopId}/media/library", () => null),
        ("cloneFromLibrary", HttpMethod.Post, $"/api/shops/{shopId}/media/library/{assetId}/clones",
            () => JsonContent.Create(new { preset = "800x600,cover" })),
        ("getAssetReferences", HttpMethod.Get, $"/api/shops/{shopId}/media/library/{assetId}/references", () => null),
        ("getDerivatives", HttpMethod.Get, $"/api/shops/{shopId}/media/library/{assetId}/derivatives", () => null),
        ("deleteFromLibrary", HttpMethod.Delete, $"/api/shops/{shopId}/media/library/{assetId}", () => null),
        ("getAssetsByIds", HttpMethod.Get, $"/api/shops/{shopId}/media/assets?ids={assetId}", () => null),
        ("getMediaUsage", HttpMethod.Get, $"/api/shops/{shopId}/media/usage", () => null),
        ("uploadShopLogo", HttpMethod.Put, $"/api/shops/{shopId}/logo", () => Multipart(withPreset: false)),
    ];

    [Theory]
    [InlineData("Customer")]
    [InlineData("Manager")]
    [InlineData("Staff")]
    [InlineData("Accountant")]
    public async Task Every_media_endpoint_rejects_non_owner_member_with_global_token_403_MEDIA_OWNER_REQUIRED(string role)
    {
        var (owner, shopId) = await _h.NewOwnerWithShopAsync();
        var library = await _h.UploadLibraryAsync(owner.Token, shopId);
        var roleId = role switch
        {
            "Customer" => WellKnownRoles.CustomerId,
            "Manager" => WellKnownRoles.ManagerId,
            "Staff" => WellKnownRoles.StaffId,
            _ => WellKnownRoles.AccountantId,
        };
        var member = await _h.NewMemberAsync(shopId, roleId);

        foreach (var e in AllEndpoints(shopId, library.Id))
        {
            var response = await _h.SendAsync(e.Method, e.Path, member.Token, e.Body());
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{role} {e.Name}: {(int)response.StatusCode}");
            var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("MEDIA_OWNER_REQUIRED", problem.GetProperty("error_code").GetString());
        }

        // Thư viện của Owner nguyên vẹn (DELETE bị chặn thật, không chỉ trả 403 sau khi đã xoá).
        var list = await _h.SendAsync(HttpMethod.Get, $"/api/shops/{shopId}/media/library", owner.Token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(library.Id.ToString(), await list.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Every_media_endpoint_rejects_user_without_membership_403_SHOP_ACCESS_DENIED_and_no_token_401()
    {
        var (owner, shopId) = await _h.NewOwnerWithShopAsync();
        var library = await _h.UploadLibraryAsync(owner.Token, shopId);
        var outsider = await _h.NewUserAsync();

        foreach (var e in AllEndpoints(shopId, library.Id))
        {
            var forbidden = await _h.SendAsync(e.Method, e.Path, outsider.Token, e.Body());
            Assert.True(forbidden.StatusCode == HttpStatusCode.Forbidden, $"outsider {e.Name}: {(int)forbidden.StatusCode}");
            Assert.Equal("SHOP_ACCESS_DENIED",
                JsonDocument.Parse(await forbidden.Content.ReadAsStringAsync()).RootElement.GetProperty("error_code").GetString());

            var anonymous = await _h.SendAsync(e.Method, e.Path, token: null, e.Body());
            Assert.True(anonymous.StatusCode == HttpStatusCode.Unauthorized, $"anonymous {e.Name}: {(int)anonymous.StatusCode}");
        }
    }

    [Fact]
    public async Task Owner_still_reads_every_media_read_endpoint()
    {
        var (owner, shopId) = await _h.NewOwnerWithShopAsync();
        var library = await _h.UploadLibraryAsync(owner.Token, shopId);

        foreach (var e in AllEndpoints(shopId, library.Id).Where(e => e.Method == HttpMethod.Get))
        {
            var response = await _h.SendAsync(e.Method, e.Path, owner.Token);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Owner {e.Name}: {(int)response.StatusCode}");
        }
    }
}
