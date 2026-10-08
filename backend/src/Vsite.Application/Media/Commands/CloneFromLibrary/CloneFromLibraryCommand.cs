using MediatR;
using Vsite.Application.Media.Dtos;

namespace Vsite.Application.Media.Commands.CloneFromLibrary;

/// <summary>T6, MEDIA-001 (#71) — `POST /shops/{shopId}/media/library/{assetId}/clones`. Nguồn load
/// trong MỘT query <c>Id == AssetId &amp;&amp; IsInLibrary</c> (Global Query Filter đã ràng `ShopId`
/// + chưa xoá) — không tìm thấy (id không tồn tại, là một clone, thuộc shop khác, hoặc đã soft
/// delete) → 404, không lộ chi tiết. Focal mặc định lấy từ bản Library nếu không truyền.</summary>
public sealed record CloneFromLibraryCommand(
    Guid ShopId,
    Guid AssetId,
    string Preset,
    float? FocalX,
    float? FocalY) : IRequest<MediaAssetDto>;
