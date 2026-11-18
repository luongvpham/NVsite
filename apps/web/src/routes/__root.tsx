import type { ReactNode } from 'react';
import type { QueryClient } from '@tanstack/react-query';
import { QueryClientProvider } from '@tanstack/react-query';
import { createRootRouteWithContext, HeadContent, Outlet, Scripts, useMatches } from '@tanstack/react-router';
import { siteLang } from '../components/site-head';
import appCss from '../styles/app.css?url';

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  head: () => ({
    meta: [
      { charSet: 'utf-8' },
      { name: 'viewport', content: 'width=device-width, initial-scale=1' },
      { title: 'vsite' },
    ],
    links: [{ rel: 'stylesheet', href: appCss }],
  }),
  component: RootComponent,
});

function RootComponent() {
  return (
    <QueryClientProvider client={Route.useRouteContext().queryClient}>
      <RootDocument>
        <Outlet />
      </RootDocument>
    </QueryClientProvider>
  );
}

function RootDocument({ children }: Readonly<{ children: ReactNode }>) {
  // `lang` theo ngôn ngữ mặc định của Shop Site (T7); trang platform giữ "vi".
  const lang = useMatches({
    select: (matches) => {
      for (const m of matches) {
        const l = siteLang(m.loaderData);
        if (l !== undefined) return l;
      }
      return undefined;
    },
  });
  return (
    <html lang={lang ?? 'vi'}>
      <head>
        <HeadContent />
      </head>
      <body>
        {children}
        <Scripts />
      </body>
    </html>
  );
}
