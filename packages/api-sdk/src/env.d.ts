// Ambient typing tối thiểu cho import.meta.env — tránh phải thêm `vite` làm dependency
// của package này chỉ để lấy type. Giá trị thật do Vite của app tiêu thụ (web/portal) inject.
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
