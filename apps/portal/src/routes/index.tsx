import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/')({
  component: HomePage,
});

function HomePage() {
  return (
    <main className="p-8">
      <h1 className="text-2xl font-semibold text-foreground">vsite portal</h1>
    </main>
  );
}
