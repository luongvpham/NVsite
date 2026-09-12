using Xunit;

namespace ArchitectureTests;

/// <summary>
/// Enforce chiều phụ thuộc Domain ← Application ← Infrastructure ← Api (backend/CLAUDE.md).
/// Rule ở đây generic — không đổi khi thêm module mới — nhưng cần một assembly module thật để
/// NetArchTest trỏ vào. Sample đã bị xoá (docs/tasks/CLEANUP-SAMPLE.md), Identity (Bước 3) chưa
/// tồn tại. File giữ nguyên, không xoá (CLEANUP-SAMPLE.md mục "Không xoá" — hạ tầng dùng lại cho
/// mọi module thật về sau), nhưng rỗng test case tạm thời.
///
/// TODO(Bước 3 — Identity): viết lại 4 test case cũ, trỏ vào Identity.Domain/.Application/.Infrastructure
/// thay vì Sample.*. Lịch sử 4 test gốc nằm ở git blame của file này (commit xoá Sample module).
/// </summary>
public sealed class LayeringTests;
