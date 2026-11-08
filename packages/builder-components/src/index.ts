export * from '../meta/manifest-schema';
export * from '../meta/prop-kinds';
export * from '../meta/value-shapes';
export * from '../generated/component-types';
export { registryMap } from '../generated/registry-map';
export { propertyPanel } from '../generated/property-panel';
// Luật + schema cho Operations Engine / editor (WEBSITE-002): vỏ cây và theme tokens (Zod), luật op theo type,
// làm sạch richText và kiểm link — một nguồn với bộ fixture dùng chung.
export { opRules } from '../generated/op-rules';
export { default as treeRules } from '../generated/tree-rules.json';
export { getPropsSchema } from './props-schemas';
export { NODE_ID_PATTERN, themeTokensSchema, treeEnvelopeSchema } from '../meta/envelope-schema';
export { isSafeLinkUrl } from './link-url';
export { sanitizeRichText } from './sanitize-html';
export * from './inspector/Inspector';
export * from './inspector/path-utils';
export * from './inspector/types';
export * from './context';
