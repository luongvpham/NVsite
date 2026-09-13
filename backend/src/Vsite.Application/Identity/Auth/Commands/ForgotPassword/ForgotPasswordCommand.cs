using MediatR;

namespace Vsite.Application.Identity.Auth.Commands.ForgotPassword;

/// <summary>
/// 03 §6.4 — LUÔN trả cùng một kết quả dù email có tồn tại/có membership ở shop hay không, để
/// không lộ thông tin email nào đã đăng ký (tránh oracle dò email). Audience/ShopId đến từ
/// `ITenantContext` (resolve theo Host), không phải field của command.
/// </summary>
public sealed record ForgotPasswordCommand(string Email) : IRequest;
