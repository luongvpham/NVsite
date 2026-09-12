import type { ComponentManifest } from '../meta/manifest-schema';

export default {
  type: 'Section',
  category: 'Layout',
  label: 'Khối chứa',
  description: 'Khối bọc các component khác, đặt được nền và khoảng cách.',
  icon: 'Square',
  acceptsChildren: true,
  allowedChildTypes: null,
  allowedInPageKinds: ['Composable'],
  maxPerPage: null,
  aiSummary: 'Khối chứa {{_childCount}} thành phần',
  since: '1.0.0',

  variants: [
    {
      key: 'Section01',
      label: 'Một cột, giới hạn bề rộng',
      preview: 'section/s01.webp',
      usesProps: ['background', 'paddingY', 'maxWidth'],
      requiresProps: [],
      since: '1.0.0',
    },
    {
      key: 'Section02',
      label: 'Tràn viền',
      preview: 'section/s02.webp',
      usesProps: ['background', 'paddingY'],
      requiresProps: [],
      since: '1.0.0',
    },
  ],

  props: {
    background: {
      kind: 'color',
      label: 'Màu nền',
      allowCustom: false,
      default: 'background',
      group: 'Hiển thị',
      order: 1,
      editableInSystemPage: false,
      since: '1.0.0',
    },
    paddingY: {
      kind: 'select',
      label: 'Khoảng cách trên dưới',
      default: 'lg',
      options: [
        { value: 'sm', label: 'Hẹp' },
        { value: 'lg', label: 'Rộng' },
      ],
      group: 'Hiển thị',
      order: 2,
      editableInSystemPage: false,
      since: '1.0.0',
    },
    maxWidth: {
      kind: 'select',
      label: 'Bề rộng tối đa',
      default: 'xl',
      options: [
        { value: 'lg', label: '1024px' },
        { value: 'xl', label: '1280px' },
      ],
      group: 'Hiển thị',
      order: 3,
      editableInSystemPage: false,
      since: '1.0.0',
    },
  },
} satisfies ComponentManifest;
