import { createFileRoute } from '@tanstack/react-router';
import { SitePage } from '../components/site-page';
import { buildSiteHead } from '../components/site-head';
import { loadSite } from '../server/load-site';

export const Route = createFileRoute('/')({
  // Host shop → trang chủ Shop Site; host platform → trang hiện tại (#104). Host đọc ở server.
  loader: () => loadSite({ data: { path: '/' } }),
  head: ({ loaderData }) => buildSiteHead(loaderData),
  component: IndexPage,
});

function IndexPage() {
  const data = Route.useLoaderData();
  return data.kind === 'platform' ? <HomePage /> : <SitePage data={data} />;
}

function HomePage() {
  return (
    <main className="p-8">
      <h1 className="text-2xl font-semibold text-foreground">vsite</h1>
      <p className="mt-2 text-muted-foreground">Bước 1 — khung monorepo.</p>
    </main>
  );
}
