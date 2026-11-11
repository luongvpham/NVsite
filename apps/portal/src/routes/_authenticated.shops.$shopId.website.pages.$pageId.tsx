import { createFileRoute, Link } from '@tanstack/react-router';
import { EditorShell } from '../components/website/editor-shell';
import { OwnerOnlyGate } from '../components/website/owner-only-gate';

export const Route = createFileRoute('/_authenticated/shops/$shopId/website/pages/$pageId')({
  component: EditorPage,
});

function EditorPage() {
  const { shopId, pageId } = Route.useParams();

  return (
    <div>
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <h1 className="text-base font-semibold text-foreground">Soạn trang</h1>
        <Link
          to="/shops/$shopId/website"
          params={{ shopId }}
          className="text-sm text-muted-foreground hover:underline"
        >
          ← Danh sách trang
        </Link>
      </div>
      {/* Gate ngoài cùng: non-Owner không mount EditorShell → không có request draft nào. */}
      <OwnerOnlyGate shopId={shopId}>
        <EditorShell shopId={shopId} pageId={pageId} />
      </OwnerOnlyGate>
    </div>
  );
}
