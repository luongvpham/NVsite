import type { ReactNode } from 'react';
import { themeStyle } from '@vsite/theme-engine';

/**
 * Khung chung của trang hệ thống Shop Site (WEBSITE-005 T8): theme MẶC ĐỊNH (`themeStyle()` = defaultThemeVars + nền/chữ/font),
 * để trang trông có chủ đích dù không có snapshot. Thuần, isomorphic (#23).
 */
export function SystemPageShell({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <div style={themeStyle()} className="flex min-h-screen items-center justify-center px-6 py-16">
      <main className="w-full max-w-md text-center">{children}</main>
    </div>
  );
}
