import { createFileRoute } from '@tanstack/react-router';
import { SamplesPage } from '../features/samples/samples-page';

export const Route = createFileRoute('/samples')({
  component: SamplesPage,
});
