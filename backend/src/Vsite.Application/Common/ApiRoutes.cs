namespace Vsite.Application.Common;

/// <summary>
/// REFACTOR-API-001 — MỌI endpoint API nằm dưới <see cref="Prefix"/>. Trước đây API mount ở root
/// (`/auth`, `/shops`) nên dùng chung namespace URL với slug shop (`vsite.vn/{slug}`) và với route
/// SPA của Portal (`/shops/new` bị Vite proxy đẩy sang BE). `api` đã có trong
/// `config/reserved-routes.json` nên không shop nào lấy được slug này.
///
/// Đặt ở Application (không phải Api) vì handler dựng link trong email (verify, reset) cũng cần nó.
/// `ApiRoutePrefixTests` khẳng định mọi endpoint thật đều bắt đầu bằng prefix này.
/// Ngoại lệ có chủ đích: `/media/*` (file ảnh public, không phải API — #53) và `/openapi/*` (chỉ dev).
/// </summary>
public static class ApiRoutes
{
    public const string Prefix = "/api";
}
