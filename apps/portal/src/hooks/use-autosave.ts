import { useEffect, useState, useSyncExternalStore } from 'react';
import { getPageDraft, savePageDraft } from '@vsite/api-sdk';
import { treeEnvelopeSchema } from '@vsite/builder-components';
import type { EditorStore } from '@vsite/builder-core';
import { AutosaveController, type AutosaveState, type FetchedDraft } from '../lib/autosave/autosave-controller';
import { getErrorMessage } from '../lib/error-messages';

/**
 * Nối `AutosaveController` vào một phiên editor. `shopId`/`pageId` chỉ từ route param (không từ dữ liệu khác).
 * Controller sống cùng phiên (tạo một lần); `start`/`dispose` ở effect để chịu được StrictMode.
 */
export function useAutosave(opts: {
  store: EditorStore;
  shopId: string;
  pageId: string;
  /** `version` nguyên văn từ GET draft lúc mở phiên. */
  initialVersion: number;
}): { controller: AutosaveController; state: AutosaveState } {
  const { store, shopId, pageId, initialVersion } = opts;

  const [controller] = useState(
    () =>
      new AutosaveController({
        store,
        initialVersion,
        save: (tree, version) => savePageDraft(shopId, pageId, { tree, version }),
        fetchDraft: async (): Promise<FetchedDraft> => {
          // Gọi thẳng, không qua cache Query: đây là "nạp lại có chủ đích" (sanitized / 409), không phải mirror.
          const draft = await getPageDraft(shopId, pageId);
          const parsed = treeEnvelopeSchema.safeParse(draft.tree);
          return { version: draft.version, tree: parsed.success ? parsed.data : null };
        },
        messageFor: getErrorMessage,
      }),
  );

  useEffect(() => {
    controller.start();
    return () => {
      controller.dispose();
    };
  }, [controller]);

  const state = useSyncExternalStore(controller.subscribe, controller.getState);
  return { controller, state };
}
