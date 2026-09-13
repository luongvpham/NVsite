namespace Identity.Domain.Enums;

/// <summary>Quyết định #37. 03 §3.4 — không ảnh hưởng Identity ngoài việc ExternalOnly không có ShopDomain/password shop.</summary>
public enum ShopKind
{
    Hosted,
    ExternalOnly,
}
