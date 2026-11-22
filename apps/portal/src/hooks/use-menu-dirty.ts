import { useGetNavigation } from '@vsite/api-sdk';

/** Menu nháp có thay đổi chưa publish (`NavigationDto.isDirty`). Chỉ đọc; chưa tải/lỗi ⇒ `false` (không chặn UI). */
export function useMenuDirty(shopId: string): boolean {
  const { data } = useGetNavigation(shopId, { query: { retry: false } });
  return data?.isDirty === true;
}
