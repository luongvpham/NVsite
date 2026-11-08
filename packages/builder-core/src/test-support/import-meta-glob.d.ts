// `import.meta.glob` do Vite/Vitest cung cấp lúc chạy test. Khai báo tối thiểu ở đây thay vì kéo `vite/client`
// (không phải dependency trực tiếp của package) hay `@types/node`.
interface ImportMeta {
  glob: (pattern: string, options: { eager: true; import: 'default' }) => Record<string, unknown>;
}
