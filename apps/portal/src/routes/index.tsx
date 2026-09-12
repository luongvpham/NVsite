import { createFileRoute, Link } from '@tanstack/react-router';

export const Route = createFileRoute('/')({
  component: HomePage,
});

function HomePage() {
  return (
    <main className="p-8">
      <h1 className="text-2xl font-semibold text-foreground">vsite portal</h1>
      <p className="mt-2 text-muted-foreground">
        Bước 2 — Component Manifest Schema + codegen. Xem{' '}
        <Link to="/dev-registry" className="text-primary underline">
          dev harness component registry
        </Link>
        .
      </p>
    </main>
  );
}
