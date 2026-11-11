import type { ReactNode } from 'react';
import { bodyFontVar, colorVar, headingFontVar } from '@vsite/theme-engine';

/**
 * Khung trang Header/Footer của renderer — NẰM NGOÀI Component Tree (05 §8), không có manifest trong Registry.
 * Style chỉ qua `var(--vs-*)` (theme bọc ở người gọi). Isomorphic (#23): không window/document, không đọc đồng hồ —
 * năm © do người gọi truyền (tránh lệch SSR↔CSR).
 */
export interface SiteLayoutProps {
  shop: { name: string; logoUrl?: string | null };
  /** href trang chủ — người gọi dựng bằng `createResolveUrl` (không hardcode, #11). */
  homeHref: string;
  /** Bước 7 (`NavigationConfig`) đổ dữ liệu; rỗng → không render `<nav>`. */
  menuItems?: readonly { label: string; href: string }[];
  /** Bước 6 để trống (Shop chưa có field, #93). */
  contact?: { address?: string; phone?: string };
  year: number;
  children: ReactNode;
}

export function SiteLayout({ shop, homeHref, menuItems, contact, year, children }: SiteLayoutProps) {
  const hasContact = Boolean(contact?.address || contact?.phone);
  return (
    <div style={{ backgroundColor: colorVar('background'), color: colorVar('text'), fontFamily: bodyFontVar }}>
      <header
        className="flex items-center justify-between gap-4 px-6 py-4"
        style={{ borderBottom: `1px solid ${colorVar('border')}`, backgroundColor: colorVar('surface') }}
      >
        <a href={homeHref} className="flex items-center gap-3" style={{ fontFamily: headingFontVar }}>
          {shop.logoUrl ? <img src={shop.logoUrl} alt="" className="h-10 w-auto" /> : null}
          <span className="text-xl font-bold">{shop.name}</span>
        </a>
        {menuItems && menuItems.length > 0 ? (
          <nav className="flex gap-4">
            {menuItems.map((item) => (
              <a key={`${item.href}|${item.label}`} href={item.href}>
                {item.label}
              </a>
            ))}
          </nav>
        ) : null}
      </header>
      <main>{children}</main>
      <footer
        className="px-6 py-8"
        style={{ borderTop: `1px solid ${colorVar('border')}`, color: colorVar('textMuted'), backgroundColor: colorVar('surface') }}
      >
        {hasContact ? (
          <address className="mb-2 not-italic">
            {contact?.address ? <div>{contact.address}</div> : null}
            {contact?.phone ? <div>{contact.phone}</div> : null}
          </address>
        ) : null}
        <p>{`© ${year} ${shop.name}`}</p>
      </footer>
    </div>
  );
}
