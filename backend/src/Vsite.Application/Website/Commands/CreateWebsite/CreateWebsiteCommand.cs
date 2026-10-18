using MediatR;
using Vsite.Application.Website.Dtos;

namespace Vsite.Application.Website.Commands.CreateWebsite;

/// <summary>
/// Tạo website trắng cho shop Hosted (T6). NGOẠI LỆ CÓ CHỦ ĐÍCH của #99: lệnh tạo aggregate gốc nhận <paramref name="ShopId"/>
/// tường minh (từ route <c>{shopId}</c>, KHÔNG từ body) — handler vẫn đối chiếu với <c>ITenantContext</c>.
/// </summary>
public sealed record CreateWebsiteCommand(Guid ShopId) : IRequest<WebsiteDto>;
