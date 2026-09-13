namespace Vsite.Domain.Identity.Enums;

/// <summary>03 §3.1 — nhánh nào của "phải có email verified HOẶC ExternalLogin Zalo" (§1 nguyên tắc 4).</summary>
public enum PrimaryIdentityKind
{
    Email,
    Zalo,
}
