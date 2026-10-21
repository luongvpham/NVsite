import { createFileRoute, Outlet } from '@tanstack/react-router';

/** Layout cho mọi màn trong một shop (`/shops/$shopId/…`): màn sửa shop là route index, Website là con. */
export const Route = createFileRoute('/_authenticated/shops/$shopId')({
  component: Outlet,
});
