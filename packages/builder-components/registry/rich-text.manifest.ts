import type { ComponentManifest } from '../meta/manifest-schema';

export default {
  type: 'RichText',
  category: 'Content',
  label: 'Đoạn văn bản',
  description: 'Văn bản định dạng: tiêu đề nhỏ, danh sách, liên kết.',
  icon: 'Type',
  acceptsChildren: false,
  allowedChildTypes: null,
  allowedInPageKinds: ['Composable', 'System'],
  maxPerPage: null,
  aiSummary: 'Văn bản: "{{_excerpt}}"',
  since: '1.0.0',

  variants: [
    {
      key: 'RichText01',
      label: 'Một cột',
      preview: 'richtext/r01.webp',
      usesProps: ['content', 'align'],
      requiresProps: ['content'],
      since: '1.0.0',
    },
  ],

  props: {
    content: {
      kind: 'richText',
      label: 'Nội dung',
      profile: 'basic',
      maxLength: 5000,
      group: 'Nội dung',
      order: 1,
      editableInSystemPage: true,
      since: '1.0.0',
    },
    align: {
      kind: 'select',
      label: 'Căn lề',
      default: 'left',
      options: [
        { value: 'left', label: 'Trái' },
        { value: 'justify', label: 'Đều hai bên' },
      ],
      group: 'Hiển thị',
      order: 1,
      editableInSystemPage: false,
      since: '1.0.0',
    },
  },
} satisfies ComponentManifest;
