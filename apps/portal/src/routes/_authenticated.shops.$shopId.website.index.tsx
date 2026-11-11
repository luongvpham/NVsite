import { createFileRoute, Link } from '@tanstack/react-router';
import { OwnerOnlyGate } from '../components/website/owner-only-gate';
import { WebsiteOverview } from '../components/website/website-overview';

export const Route = createFileRoute('/_authenticated/shops/$shopId/website/')({
  component: WebsitePage,
});

function WebsitePage() {
  const { shopId } = Route.useParams();

  return (
    <main className="mx-auto max-w-3xl space-y-6 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-foreground">Website</h1>
        <Link to="/shops/$shopId" params={{ shopId }} className="text-sm text-muted-foreground hover:underline">
          ← Về shop
        </Link>
      </div>
      <OwnerOnlyGate shopId={shopId}>
        <WebsiteOverview shopId={shopId} />
      </OwnerOnlyGate>
    </main>
  );
}
