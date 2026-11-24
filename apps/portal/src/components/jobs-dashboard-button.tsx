import { useState, useSyncExternalStore } from 'react';
import { Button } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { getAccessTokenRoles, subscribeAccessToken, useCreateJobsDashboardSession } from '@vsite/api-sdk';

const PLATFORM_ADMIN_ROLE = 'PlatformAdmin';
const DENIED_MESSAGE = 'Bạn không có quyền xem job nền';
const GENERIC_ERROR_MESSAGE = 'Không mở được dashboard job nền, vui lòng thử lại.';

/**
 * Mutator reject thẳng body ProblemDetails (không còn status HTTP) còn lỗi mạng thì reject AxiosError thô —
 * nên đọc 403 qua cả ba đường: AxiosError.response.status, ProblemDetails.status, error_code của endpoint này.
 */
function isForbidden(error: unknown): boolean {
  if (typeof error === 'object' && error !== null) {
    // AxiosError thô (portal không phụ thuộc axios trực tiếp → đọc theo hình dạng).
    if ('response' in error && typeof error.response === 'object' && error.response !== null) {
      return 'status' in error.response && error.response.status === 403;
    }
    if ('status' in error && error.status === 403) return true;
  }
  const code = getErrorCode(error);
  return code === 'PLATFORM_ADMIN_REQUIRED' || code === 'TOKEN_SHOP_MISMATCH';
}

/**
 * JOBS-001 F1 (#114) — mở dashboard Hangfire cho PlatformAdmin. Claim `role` trong access token CHỈ để
 * hiện/ẩn nút (quyền thật do BE kiểm → 403). POST đi đường dẫn tương đối qua proxy để cookie host-only
 * rơi đúng host `admin.*`, rồi mở `/hangfire` ở tab mới (cùng origin).
 */
export function JobsDashboardButton() {
  const roles = useSyncExternalStore(subscribeAccessToken, getAccessTokenRoles);
  const [denied, setDenied] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const mutation = useCreateJobsDashboardSession({
    mutation: {
      onMutate: () => {
        setMessage(null);
      },
      onSuccess: () => {
        window.open('/hangfire', '_blank');
      },
      onError: (error) => {
        if (isForbidden(error)) {
          setDenied(true);
          setMessage(DENIED_MESSAGE);
        } else {
          setMessage(GENERIC_ERROR_MESSAGE);
        }
      },
    },
  });

  const isAdmin = roles.includes(PLATFORM_ADMIN_ROLE);

  return (
    <>
      {isAdmin && !denied && (
        <Button
          variant="outline"
          size="sm"
          disabled={mutation.isPending}
          onClick={() => {
            mutation.mutate();
          }}
        >
          Job nền
        </Button>
      )}
      {message && (
        <span className="text-sm text-destructive" role="alert">
          {message}
        </span>
      )}
    </>
  );
}
