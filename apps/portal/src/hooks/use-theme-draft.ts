import { useCallback, useEffect, useMemo, useState } from 'react';
import { useStore } from 'zustand';
import { useGetTheme, useUpdateTheme } from '@vsite/api-sdk';
import { themeTokensSchema } from '@vsite/builder-components';
import type { EditorStore } from '@vsite/builder-core';
import { getErrorCode, getProblemParams, isTreeValidationParams } from '@vsite/shared';
import { getErrorMessage } from '../lib/error-messages';
import { THEME_FIELD_KEYS } from '../lib/theme-fields';
import { mapThemeErrors, setThemeValue, themeEquals, type MappedThemeErrors, type ThemeFieldKey } from '../lib/theme-edit';

interface Saved {
  tokens: unknown;
  /** `presetKey` giữ nguyên khi lưu (plan F9): panel không đổi preset. */
  presetKey: string | null;
}

export interface ThemeSaveError {
  code: string | undefined;
  message: string;
  mapped: MappedThemeErrors | undefined;
}

export type ThemeLoadState =
  | { status: 'loading' }
  | { status: 'error'; code: string | undefined; message: string; retry: () => void }
  | { status: 'invalid' }
  | { status: 'ready' };

export interface ThemeDraftApi {
  load: ThemeLoadState;
  /** Theme đang sửa (cũng là cái canvas đang vẽ). */
  tokens: unknown;
  dirty: boolean;
  saving: boolean;
  saveError: ThemeSaveError | undefined;
  /** Đổi một ô; trả thông báo nếu Engine từ chối (vd. theme quá lớn). */
  setValue: (key: ThemeFieldKey, value: string) => string | null;
  save: () => void;
  revert: () => void;
}

/**
 * Theme của website trong một phiên editor (Flow 6). Theme là state của website chứ không của trang: GET một lần lúc mở
 * phiên → `loadTheme` (canvas thấy ngay), sửa qua op `changeTheme` (ngoài lịch sử undo của cây), "bản đã lưu" giữ ở đây để so và hoàn tác.
 * `themeDraft` của store là nguồn của canvas — KHÔNG copy ngược từ Query sau lần nạp đầu (#20).
 */
export function useThemeDraft(store: EditorStore, shopId: string): ThemeDraftApi {
  const query = useGetTheme(shopId, {
    query: {
      retry: false,
      staleTime: Infinity,
      refetchOnWindowFocus: false,
      refetchOnReconnect: false,
      // Giống getPageDraft: cache chỉ đúng lúc mở phiên, không thì mở lại với theme cũ.
      gcTime: 0,
    },
  });
  const mutation = useUpdateTheme();

  const parsed = useMemo(
    () => (query.data ? themeTokensSchema.safeParse(query.data.tokens) : undefined),
    [query.data],
  );

  const [saved, setSaved] = useState<Saved | null>(null);
  const [saveError, setSaveError] = useState<ThemeSaveError | undefined>();

  // Nạp một lần khi dữ liệu server về và đọc được.
  useEffect(() => {
    if (!parsed?.success || !query.data || saved) return;
    setSaved({ tokens: parsed.data, presetKey: query.data.presetKey });
    store.getState().loadTheme(parsed.data);
  }, [parsed, query.data, saved, store]);

  const tokens = useStore(store, (s) => s.themeDraft);
  const dirty = saved !== null && !themeEquals(tokens, saved.tokens);

  const setValue = useCallback(
    (key: ThemeFieldKey, value: string): string | null => {
      const result = store
        .getState()
        .dispatch({ type: 'changeTheme', tokens: setThemeValue(store.getState().themeDraft, key, value) });
      setSaveError(undefined);
      return result.ok ? null : result.message;
    },
    [store],
  );

  const revert = useCallback(() => {
    if (!saved) return;
    store.getState().loadTheme(saved.tokens);
    setSaveError(undefined);
  }, [saved, store]);

  const save = useCallback(() => {
    if (!saved || mutation.isPending) return;
    const sent = store.getState().themeDraft;
    setSaveError(undefined);
    mutation.mutate(
      { shopId, data: { presetKey: saved.presetKey, tokens: sent as never } },
      {
        onSuccess: (response) => {
          setSaved({ tokens: sent, presetKey: response.presetKey });
        },
        onError: (error) => {
          const code = getErrorCode(error);
          const params = getProblemParams(error, isTreeValidationParams);
          setSaveError({
            code,
            message: getErrorMessage(code),
            mapped: params ? mapThemeErrors(params.errors, KNOWN_FIELDS) : undefined,
          });
        },
      },
    );
  }, [mutation, saved, shopId, store]);

  let load: ThemeLoadState;
  if (query.isPending) load = { status: 'loading' };
  else if (query.isError) {
    const code = getErrorCode(query.error);
    load = { status: 'error', code, message: getErrorMessage(code), retry: () => void query.refetch() };
  } else if (!parsed?.success) load = { status: 'invalid' };
  else load = { status: 'ready' };

  return { load, tokens, dirty, saving: mutation.isPending, saveError, setValue, save, revert };
}

const KNOWN_FIELDS: ReadonlySet<ThemeFieldKey> = new Set(THEME_FIELD_KEYS);
