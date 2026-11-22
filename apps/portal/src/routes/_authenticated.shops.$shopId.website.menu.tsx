import { createFileRoute, Link } from '@tanstack/react-router';
import { NavigationEditor } from '../components/website/navigation-editor';
import { OwnerOnlyGate } from '../components/website/owner-only-gate';

export const Route = createFileRoute('/_authenticated/shops/$shopId/website/menu')({
  component: MenuPage,
});

function MenuPage() {
  const { shopId } = Route.useParams();

  return (
    <main className="mx-auto max-w-3xl space-y-6 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-foreground">Menu website</h1>
        <Link
          to="/shops/$shopId/website"
          params={{ shopId }}
          className="text-sm text-muted-foreground hover:underline"
        >
          ← Về website
        </Link>
      </div>
      {/* Gate ngoài cùng: non-Owner không mount editor → không có request menu nào. */}
      <OwnerOnlyGate shopId={shopId}>
        <NavigationEditor shopId={shopId} />
      </OwnerOnlyGate>
    </main>
  );
}
