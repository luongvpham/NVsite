import axios from 'axios';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  getAccessTokenRoles,
  restoreSession,
  setAccessToken,
  subscribeAccessToken,
} from '../src/mutator/axios-instance';

// JOBS-001 F1 — Portal chỉ HIỆN/ẨN nút theo claim `role` của access token hiện tại (#114). Decode payload, không verify.

const b64url = (value: string) => {
  const bytes = new TextEncoder().encode(value);
  return btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
};
const jwt = (payload: unknown) => `${b64url('{"alg":"none"}')}.${b64url(JSON.stringify(payload))}.sig`;

afterEach(() => {
  setAccessToken(undefined);
  vi.restoreAllMocks();
});

describe('getAccessTokenRoles', () => {
  it('role là chuỗi → mảng một phần tử', () => {
    setAccessToken(jwt({ role: 'PlatformAdmin' }));
    expect(getAccessTokenRoles()).toEqual(['PlatformAdmin']);
  });

  it('role là mảng → giữ nguyên', () => {
    setAccessToken(jwt({ role: ['PlatformAdmin', 'Other'] }));
    expect(getAccessTokenRoles()).toEqual(['PlatformAdmin', 'Other']);
  });

  it('mảng lẫn phần tử không phải chuỗi → chỉ giữ chuỗi', () => {
    setAccessToken(jwt({ role: ['PlatformAdmin', 1, null] }));
    expect(getAccessTokenRoles()).toEqual(['PlatformAdmin']);
  });

  it('không có claim role / role sai kiểu → []', () => {
    setAccessToken(jwt({ sub: 'u1' }));
    expect(getAccessTokenRoles()).toEqual([]);
    setAccessToken(jwt({ role: 42 }));
    expect(getAccessTokenRoles()).toEqual([]);
  });

  it('không có token → []', () => {
    expect(getAccessTokenRoles()).toEqual([]);
  });

  it.each([
    ['không phải JWT', 'not-a-jwt'],
    ['base64 sai', 'a.@@@@.c'],
    ['payload không phải JSON', `a.${b64url('xin chao')}.c`],
    ['payload là JSON nhưng không phải object', `a.${b64url('"PlatformAdmin"')}.c`],
    ['payload null', `a.${b64url('null')}.c`],
  ])('token hỏng (%s) → [] và không ném', (_name, token) => {
    setAccessToken(token);
    expect(() => getAccessTokenRoles()).not.toThrow();
    expect(getAccessTokenRoles()).toEqual([]);
  });

  it('base64url có "-" / "_" và thiếu padding vẫn đọc được', () => {
    // Chọn giá trị để base64 chuẩn chứa "+" "/" ("?>>" → "Pz4+", "???" → "Pz8/"), và độ dài không chia hết 4.
    const role = '???>>>~~~ô';
    const raw = JSON.stringify({ role });
    const encoded = b64url(raw);
    expect(/[-_]/.test(encoded) && encoded.length % 4 !== 0).toBe(true);
    setAccessToken(`h.${encoded}.s`);
    expect(getAccessTokenRoles()).toEqual([role]);
  });

  it('snapshot ổn định: cùng token → cùng tham chiếu mảng (cho useSyncExternalStore)', () => {
    setAccessToken(jwt({ role: 'PlatformAdmin' }));
    expect(getAccessTokenRoles()).toBe(getAccessTokenRoles());
    setAccessToken(undefined);
    expect(getAccessTokenRoles()).toBe(getAccessTokenRoles());
  });
});

describe('subscribeAccessToken', () => {
  it('được gọi khi setAccessToken đổi token, và khi clear', () => {
    const cb = vi.fn();
    const off = subscribeAccessToken(cb);
    setAccessToken(jwt({ role: 'PlatformAdmin' }));
    expect(cb).toHaveBeenCalledTimes(1);
    setAccessToken(undefined);
    expect(cb).toHaveBeenCalledTimes(2);
    off();
    setAccessToken(jwt({}));
    expect(cb).toHaveBeenCalledTimes(2);
  });

  it('refresh thành công → subscriber được gọi, roles theo token mới', async () => {
    setAccessToken(jwt({}));
    const cb = vi.fn();
    const off = subscribeAccessToken(cb);
    vi.spyOn(axios, 'post').mockResolvedValue({ data: { accessToken: jwt({ role: 'PlatformAdmin' }) } });

    await restoreSession();

    expect(cb).toHaveBeenCalledTimes(1);
    expect(getAccessTokenRoles()).toEqual(['PlatformAdmin']);
    off();
  });

  it('refresh hỏng → token bị xoá, subscriber được gọi, roles = []', async () => {
    setAccessToken(jwt({ role: 'PlatformAdmin' }));
    const cb = vi.fn();
    const off = subscribeAccessToken(cb);
    vi.spyOn(axios, 'post').mockRejectedValue(new Error('401'));

    await restoreSession();

    expect(cb).toHaveBeenCalledTimes(1);
    expect(getAccessTokenRoles()).toEqual([]);
    off();
  });
});

describe('refresh và epoch phiên', () => {
  function deferredRefresh() {
    let resolve!: (token: string) => void;
    let reject!: (error: Error) => void;
    const promise = new Promise<{ data: { accessToken: string } }>((res, rej) => {
      resolve = (accessToken) => {
        res({ data: { accessToken } });
      };
      reject = rej;
    });
    vi.spyOn(axios, 'post').mockReturnValue(promise);
    return { resolve, reject };
  }

  it('refresh hỏng sau khi phiên đã clear + đăng nhập mới → KHÔNG xoá token mới', async () => {
    setAccessToken(jwt({}));
    const refresh = deferredRefresh();
    const pending = restoreSession();

    setAccessToken(undefined);
    const newToken = jwt({ role: 'PlatformAdmin' });
    setAccessToken(newToken);
    const cb = vi.fn();
    const off = subscribeAccessToken(cb);
    const rolesBefore = getAccessTokenRoles();

    refresh.reject(new Error('401'));
    await pending;

    expect(cb).not.toHaveBeenCalled();
    expect(getAccessTokenRoles()).toBe(rolesBefore);
    expect(getAccessTokenRoles()).toEqual(['PlatformAdmin']);
    off();
  });

  it('refresh thành công sau khi phiên đã clear → token không được hồi sinh, subscriber không bị gọi', async () => {
    setAccessToken(jwt({ role: 'PlatformAdmin' }));
    const refresh = deferredRefresh();
    const pending = restoreSession();

    setAccessToken(undefined);
    const cb = vi.fn();
    const off = subscribeAccessToken(cb);

    refresh.resolve(jwt({ role: 'PlatformAdmin' }));
    await expect(pending).resolves.toBeUndefined();

    expect(cb).not.toHaveBeenCalled();
    expect(getAccessTokenRoles()).toEqual([]);
    off();
  });
});
