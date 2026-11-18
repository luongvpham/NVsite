import { createFileRoute } from '@tanstack/react-router';
import { serveSeoFile } from '../server/seo-route';
import { SeoFileKind } from '../server/site-seo-files';

export const Route = createFileRoute('/sitemap.xml')({
  server: { handlers: { GET: () => serveSeoFile(SeoFileKind.Sitemap) } },
});
