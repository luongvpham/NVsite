import { createFileRoute } from '@tanstack/react-router';
import { PlatformNotFound, SitePage } from '../components/site-page';
import { buildSiteHead } from '../components/site-head';
import { loadSite } from '../server/load-site';

export const Route = createFileRoute('/$')({
  // Chỉ gửi path; shop chọn theo Host ở server (R13). Khớp slug → trang là việc của T5.
  loader: ({ location }) => loadSite({ data: { path: location.pathname } }),
  head: ({ loaderData }) => buildSiteHead(loaderData),
  component: SplatPage,
});

function SplatPage() {
  const data = Route.useLoaderData();
  return data.kind === 'platform' ? <PlatformNotFound /> : <SitePage data={data} />;
}
