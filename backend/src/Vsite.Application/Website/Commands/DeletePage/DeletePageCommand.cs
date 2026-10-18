using MediatR;

namespace Vsite.Application.Website.Commands.DeletePage;

/// <summary>Xoá mềm một trang Composable không phải trang chủ. Trang chủ/System → 409 <c>PAGE_NOT_DELETABLE</c> (kiểm ở handler, không dựa FK — `05` §4).</summary>
public sealed record DeletePageCommand(Guid PageId) : IRequest;
