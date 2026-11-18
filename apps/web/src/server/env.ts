import '@tanstack/react-start/server-only';

export interface ServerEnv {
  /** Origin BE (vd http://localhost:5270). Chỉ server — không có prefix VITE_. */
  apiOrigin: string;
  /** Domain gốc của Shop Site: `{slug}.{rootDomain}`. */
  rootDomain: string;
}

function required(name: 'API_ORIGIN' | 'ROOT_DOMAIN'): string {
  const value = process.env[name]?.trim();
  if (!value) {
    throw new Error(`Thiếu biến môi trường ${name} (xem apps/web/.env.example).`);
  }
  return value;
}

/** Chỉ ROOT_DOMAIN — phân loại host không cần BE (nền tảng `/` không được 500 vì thiếu API_ORIGIN). */
export function readRootDomain(): string {
  return required('ROOT_DOMAIN');
}

/** Chỉ API_ORIGIN — đọc lúc thật sự gọi BE. */
export function readApiOrigin(): string {
  return required('API_ORIGIN');
}

/** Đọc lười (mỗi lần gọi) để test và `vite build` không vỡ khi thiếu env; không có mặc định ngầm (A2). */
export function readServerEnv(): ServerEnv {
  return { apiOrigin: required('API_ORIGIN'), rootDomain: required('ROOT_DOMAIN') };
}
