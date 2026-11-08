import type { TreeEnvelope, TreeNode } from '../tree/types';

/** Dựng vỏ cây hợp lệ từ danh sách node gốc — dùng chung cho test của package. */
export function envelope(children: TreeNode[] = []): TreeEnvelope {
  return { schemaVersion: 1, id: 'root', type: 'Page', children };
}

export const hero = (id = 'c_hero1', props?: Record<string, unknown>): TreeNode => ({
  id,
  type: 'Hero',
  variant: 'Hero01',
  ...(props ? { props } : {}),
});

export const richText = (id = 'c_rt001', content = '<p>a</p>'): TreeNode => ({
  id,
  type: 'RichText',
  variant: 'RichText01',
  props: { content },
});

export const section = (id = 'c_sec01', children: TreeNode[] = []): TreeNode => ({
  id,
  type: 'Section',
  variant: 'Section01',
  children,
});

/** Section(RichText) + Hero — cây mẫu cho test truy vấn. */
export function sampleTree(): TreeEnvelope {
  return envelope([section('c_sec01', [richText('c_rt001')]), hero('c_hero1')]);
}
