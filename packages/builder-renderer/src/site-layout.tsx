import type { ReactNode } from 'react';
import { bodyFontVar, colorVar, headingFontVar, radiusVar, shadowVar } from '@vsite/theme-engine';

/**
 * Khung trang Header/Footer của renderer — NẰM NGOÀI Component Tree (05 §8), không có manifest trong Registry.
 * Style chỉ qua `var(--vs-*)` (theme bọc ở người gọi). Isomorphic (#23): không window/document, không đọc đồng hồ —
 * năm © do người gọi truyền (tránh lệch SSR↔CSR).
 */
/** Mục menu (đệ quy, tới 3 cấp) — tương thích đầu ra `resolveNavigation` (builder-components). */
export interface SiteMenuItem {
  label: string;
  href: string;
  external?: boolean;
  children?: readonly SiteMenuItem[];
}

export interface SiteLayoutProps {
  shop: { name: string; logoUrl?: string | null };
  /** href trang chủ — người gọi dựng bằng `createResolveUrl` (không hardcode, #11). */
  homeHref: string;
  /** Bước 7 (`NavigationConfig`) đổ dữ liệu; rỗng → không render `<nav>`. */
  menuItems?: readonly SiteMenuItem[];
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
          <nav aria-label="Menu chính" className="vs-nav">
            <style>{NAV_CSS}</style>
            <ul className="vs-nav-desktop">
              {menuItems.map((item, i) => (
                <DesktopItem key={`${item.href}|${item.label}|${i}`} item={item} />
              ))}
            </ul>
            <details className="vs-nav-mobile">
              <summary aria-label="Mở menu">☰</summary>
              <ul>
                {menuItems.map((item, i) => (
                  <MobileItem key={`${item.href}|${item.label}|${i}`} item={item} />
                ))}
              </ul>
            </details>
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

/**
 * Menu thuần CSS/HTML, không JS (#23, R7): desktop mở con bằng `:hover`/`:focus-within`, mobile dùng `<details>`.
 * Nhãn render dạng text (React escape) — không dangerouslySetInnerHTML (R4).
 */
const NAV_CSS = `
.vs-nav ul{list-style:none;margin:0;padding:0}
.vs-nav a{color:inherit;text-decoration:none}
.vs-nav-desktop{display:flex;gap:1rem}
.vs-nav-desktop li{position:relative}
.vs-nav-desktop li>ul{display:none;position:absolute;top:100%;left:0;min-width:12rem;z-index:10;padding:.5rem 0;background-color:${colorVar('surface')};border:1px solid ${colorVar('border')};border-radius:${radiusVar};box-shadow:${shadowVar}}
.vs-nav-desktop li>ul li>ul{top:0;left:100%}
.vs-nav-desktop li:hover>ul,.vs-nav-desktop li:focus-within>ul{display:block}
.vs-nav-desktop li>ul a{display:block;padding:.375rem 1rem;white-space:nowrap}
.vs-nav-mobile{display:none}
.vs-nav-mobile summary{cursor:pointer;list-style:none;padding:.25rem .5rem;color:${colorVar('text')}}
.vs-nav-mobile summary::-webkit-details-marker{display:none}
.vs-nav-mobile>ul{position:absolute;left:0;right:0;z-index:10;padding:.5rem 1.5rem;background-color:${colorVar('surface')};border-bottom:1px solid ${colorVar('border')}}
.vs-nav-mobile li{padding:.25rem 0}
.vs-nav-mobile li ul{padding-left:1rem}
.vs-nav-mobile li.vs-has-sub{position:relative}
.vs-nav-mobile li.vs-has-sub>details>summary{position:absolute;right:0;top:.25rem;padding:.25rem .75rem}
.vs-nav-mobile li.vs-has-sub>details[open]>summary{transform:rotate(90deg)}
@media (max-width:767px){.vs-nav-desktop{display:none}.vs-nav-mobile{display:block}}
`;

function ItemLink({ item }: { item: SiteMenuItem }) {
  return item.external ? (
    <a href={item.href} target="_blank" rel="noopener noreferrer">
      {item.label}
    </a>
  ) : (
    <a href={item.href}>{item.label}</a>
  );
}

function DesktopItem({ item }: { item: SiteMenuItem }) {
  const kids = item.children ?? [];
  return (
    <li>
      <ItemLink item={item} />
      {kids.length > 0 ? (
        <ul>
          {kids.map((c, i) => (
            <DesktopItem key={`${c.href}|${c.label}|${i}`} item={c} />
          ))}
        </ul>
      ) : null}
    </li>
  );
}

function MobileItem({ item }: { item: SiteMenuItem }) {
  const kids = item.children ?? [];
  if (kids.length === 0) {
    return (
      <li>
        <ItemLink item={item} />
      </li>
    );
  }
  return (
    <li className="vs-has-sub">
      <ItemLink item={item} />
      <details>
        <summary aria-label={`Mở menu con ${item.label}`}>▸</summary>
        <ul>
          {kids.map((c, i) => (
            <MobileItem key={`${c.href}|${c.label}|${i}`} item={c} />
          ))}
        </ul>
      </details>
    </li>
  );
}
