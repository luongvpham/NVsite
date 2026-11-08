// Operations Engine phía client (#14, #42): 5 op + validate theo 05 §11 + apply (Immer) + store undo/redo (zundo).
// CHỈ dùng bởi apps/portal — builder-renderer KHÔNG import package này (renderer phải chạy được ở apps/web).
export * from './tree/types';
export * from './tree/query';
export * from './tree/limits';
export { createNode, generateNodeId } from './tree/new-node';
export * from './operations/types';
export * from './operations/result';
export { applyOperation, executeOperation, type ExecuteResult } from './operations/apply';
export { validateOperation, validateThemeTokens, type ValidationContext } from './operations/validate';
export { setIn } from './operations/paths';
export { createEditorStore, type EditorState, type EditorStore } from './store/editor-store';
