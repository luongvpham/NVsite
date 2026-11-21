import { useMemo, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  closestCenter,
  closestCorners,
  DndContext,
  getFirstCollision,
  KeyboardSensor,
  MeasuringStrategy,
  PointerSensor,
  useSensor,
  useSensors,
  type Announcements,
  type DragEndEvent,
  type DragMoveEvent,
  type DragOverEvent,
  type DragStartEvent,
  type DroppableContainer,
  type KeyboardCoordinateGetter,
  type UniqueIdentifier,
} from '@dnd-kit/core';
import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import {
  getGetNavigationQueryKey,
  useGetNavigation,
  useListPages,
  useSaveNavigation,
  type NavigationDto,
  type PageDto,
} from '@vsite/api-sdk';
import { getErrorCode } from '@vsite/shared';
import { Button } from '@vsite/ui';
import { getErrorMessage } from '../../lib/error-messages';
import {
  applyMove,
  findItem,
  flatten,
  fromDto,
  getProjection,
  isPageAllowedInMenu,
  isPageKey,
  NAV_LIMITS,
  newUid,
  pageIdOf,
  pageIdsInMenu,
  pageKeyOf,
  removeDescendants,
  removeItem,
  toRequestItems,
  uidAtPointer,
  updateItem,
  validateMenu,
  MENU_ERROR_KEY,
  type FlatNavItem,
  type NavItem,
} from './navigation-tree';

/** Độ thụt mỗi cấp (px) — cũng là bước ngang của kéo chuột/bàn phím để đổi cấp. */
const INDENT = 24;

/**
 * Màn Menu (WEBSITE-007 F4): sắp menu website tối đa 3 cấp. Tải menu (GET) + danh sách trang (tên trang, trạng
 * thái bật/tắt); khi 409 thì tải lại và dựng lại form từ bản mới (đổi `key` ⇒ bỏ mọi thay đổi chưa lưu).
 */
function topLevelOverMessage(n: number): string {
  return `Menu đang có ${String(n)} mục cấp 1, tối đa ${String(NAV_LIMITS.topLevel)} — xoá bớt mục (kể cả mục đang ẩn) hoặc kéo vào nhóm.`;
}

export function NavigationEditor({ shopId }: { shopId: string }) {
  // Cùng chính sách với các editor nháp khác (editor-shell, use-theme-draft): bản trong cache chỉ đúng lúc mở màn
  // (sau đó version đổi theo mỗi lần lưu). Không tự refetch đè lên form; bỏ cache ngay khi rời màn để lần mở sau
  // GET lại — không thì mở lại với `version` cũ ⇒ 409 giả và mất thay đổi. `invalidateQueries` (sau lưu, F5 sau
  // publish/rollback) và `refetch()` khi 409 vẫn chạy bình thường.
  const navigationQuery = useGetNavigation(shopId, {
    query: {
      retry: false,
      staleTime: Infinity,
      refetchOnWindowFocus: false,
      refetchOnReconnect: false,
      gcTime: 0,
      // `gcTime` của các observer cùng key (tổng quan, preview) được gộp bằng max ⇒ `gcTime: 0` không đủ để bỏ cache.
      refetchOnMount: 'always',
    },
  });
  const pagesQuery = useListPages(shopId);
  const [loadKey, setLoadKey] = useState(0);
  const [notice, setNotice] = useState<string | null>(null);

  const navigation = navigationQuery.data;
  const pageList = pagesQuery.data;

  // Màn lỗi / đang tải chỉ khi CHƯA có dữ liệu (lần tải đầu). Refetch nền lỗi (TanStack v5 giữ `data` và bật
  // `isError`) không được thay form bằng màn lỗi — sẽ mất bản đang sửa.
  // Form seed một lần từ `initial` ⇒ chỉ dựng sau lần GET khi mở màn, không dựng từ cache cũ của observer khác.
  if (navigation === undefined || !navigationQuery.isFetchedAfterMount || pageList === undefined) {
    if (!navigationQuery.isError && !pagesQuery.isError) {
      return <p className="text-muted-foreground">Đang tải menu...</p>;
    }
    const error = navigationQuery.isError ? navigationQuery.error : pagesQuery.error;
    return (
      <div className="space-y-3">
        <p role="alert" className="text-destructive">
          {getErrorMessage(getErrorCode(error))}
        </p>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => {
            if (navigationQuery.isError) void navigationQuery.refetch();
            if (pagesQuery.isError) void pagesQuery.refetch();
          }}
        >
          Thử lại
        </Button>
      </div>
    );
  }

  const reloadAfterConflict = async () => {
    const result = await navigationQuery.refetch();
    if (result.isError) {
      // Không tải được bản mới: giữ form (version cũ sẽ lại 409), báo để người dùng tải lại trang.
      setNotice(
        `Menu vừa được sửa ở nơi khác, nhưng chưa tải lại được bản mới nhất: ${getErrorMessage(getErrorCode(result.error))}`,
      );
      return;
    }
    setNotice(
      'Menu vừa được sửa ở nơi khác. Đã tải lại bản mới nhất — thay đổi chưa lưu của bạn đã bị bỏ.',
    );
    setLoadKey((key) => key + 1);
  };

  return (
    <NavigationForm
      key={loadKey}
      shopId={shopId}
      initial={navigation}
      pages={pageList}
      notice={notice}
      onSaved={() => {
        setNotice(null);
      }}
      onConflict={reloadAfterConflict}
    />
  );
}

interface FormProps {
  shopId: string;
  initial: NavigationDto;
  pages: PageDto[];
  notice: string | null;
  onSaved: () => void;
  onConflict: () => Promise<void>;
}

interface DragState {
  activeUid: string;
  overUid: string;
  offsetX: number;
}

interface NavigationErrorItem {
  path: string;
  code: string;
  message: string;
}

/** `params` của 422 `NAVIGATION_INVALID` (`truncated` có thể thiếu — không bắt buộc để đọc được lỗi). */
function readNavigationErrors(
  error: unknown,
): { errors: NavigationErrorItem[]; truncated: boolean } | null {
  if (typeof error !== 'object' || error === null || !('params' in error)) return null;
  const params: unknown = error.params;
  if (
    typeof params !== 'object' ||
    params === null ||
    !('errors' in params) ||
    !Array.isArray(params.errors)
  )
    return null;
  const errors = (params.errors as unknown[]).filter(
    (e): e is NavigationErrorItem =>
      typeof e === 'object' &&
      e !== null &&
      typeof (e as Record<string, unknown>).path === 'string' &&
      typeof (e as Record<string, unknown>).code === 'string',
  );
  const truncated = 'truncated' in params && params.truncated === true;
  return { errors, truncated };
}

const messageOf = (code: string, message?: string) => (message ? message : getErrorMessage(code));

function NavigationForm({ shopId, initial, pages, notice, onSaved, onConflict }: FormProps) {
  const queryClient = useQueryClient();
  const [items, setItems] = useState<NavItem[]>(() => fromDto(initial.items));
  // Lần lưu đầu gửi `version` của GET (có thể null); các lần sau gửi `version` của kết quả PUT gần nhất.
  const [version, setVersion] = useState<number | null>(initial.version);
  const [rowErrors, setRowErrors] = useState<Map<string, string[]>>(new Map());
  const [generalError, setGeneralError] = useState<{ message: string; details: string[] } | null>(
    null,
  );
  const [saved, setSaved] = useState(false);
  const [drag, setDrag] = useState<DragState | null>(null);
  const [dropError, setDropError] = useState<string | null>(null);
  const [pageToAdd, setPageToAdd] = useState('');

  const pagesById = useMemo(() => new Map(pages.map((p) => [p.id.toLowerCase(), p])), [pages]);
  const flat = useMemo(() => {
    const all = flatten(items);
    return drag ? removeDescendants(all, drag.activeUid) : all;
  }, [items, drag]);
  const projection = drag
    ? getProjection(flat, drag.activeUid, drag.overUid, drag.offsetX, INDENT)
    : null;

  // Coordinate getter của bàn phím đọc trạng thái mới nhất qua ref (sensor được tạo một lần).
  const sensorContext = useRef<{ flat: FlatNavItem[]; offsetX: number }>({ flat, offsetX: 0 });
  sensorContext.current = { flat, offsetX: drag?.offsetX ?? 0 };

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: treeKeyboardCoordinates(sensorContext) }),
  );

  const saveMutation = useSaveNavigation();
  // Đếm số lần sửa: lưu xong chỉ báo "Đã lưu" nếu không có thay đổi nào sau lúc gửi.
  const editSeq = useRef(0);
  const itemsRef = useRef(items);
  itemsRef.current = items;
  const topLevelFull = items.length >= NAV_LIMITS.topLevel;
  const topLevelOver = items.length > NAV_LIMITS.topLevel;
  const usedPageIds = pageIdsInMenu(items);
  const addablePages = pages.filter(
    (p) => isPageAllowedInMenu(p) && !usedPageIds.has(p.id.toLowerCase()),
  );

  const nameOf = (item: NavItem): string => {
    if (isPageKey(item.key))
      return pagesById.get(pageIdOf(item.key))?.title ?? 'Trang không còn tồn tại';
    return item.label ?? '';
  };

  const edit = (next: NavItem[], touchedUid?: string) => {
    editSeq.current += 1;
    setItems(next);
    setSaved(false);
    if (touchedUid && rowErrors.has(touchedUid)) {
      const copy = new Map(rowErrors);
      copy.delete(touchedUid);
      setRowErrors(copy);
    }
  };

  const save = () => {
    setSaved(false);
    setDropError(null);
    const clientErrors = validateMenu(items, pagesById);
    if (clientErrors.size > 0) {
      setRowErrors(
        new Map(
          [...clientErrors]
            .filter(([uid]) => uid !== MENU_ERROR_KEY)
            .map(([uid, codes]) => [uid, codes.map((c) => getErrorMessage(c))]),
        ),
      );
      setGeneralError({
        message: getErrorMessage('NAVIGATION_INVALID'),
        details: clientErrors.has(MENU_ERROR_KEY) ? [topLevelOverMessage(items.length)] : [],
      });
      return;
    }
    setRowErrors(new Map());
    setGeneralError(null);
    // Giữ đúng mảng đã gửi: JSON Pointer của 422 trỏ vào body này, không phải cây đang sửa tiếp.
    const sent = items;
    const seqAtSend = editSeq.current;
    saveMutation.mutate(
      { shopId, data: { version, items: toRequestItems(sent) } },
      {
        onSuccess: (result) => {
          setVersion(result.version);
          setSaved(editSeq.current === seqAtSend);
          onSaved();
          void queryClient.invalidateQueries({ queryKey: getGetNavigationQueryKey(shopId) });
        },
        onError: (error) => {
          const code = getErrorCode(error);
          if (code === 'NAVIGATION_CONFLICT') {
            void onConflict();
            return;
          }
          if (code === 'NAVIGATION_INVALID') {
            const parsed = readNavigationErrors(error);
            const byRow = new Map<string, string[]>();
            const details: string[] = [];
            for (const e of parsed?.errors ?? []) {
              const uid = uidAtPointer(sent, e.path);
              const text = messageOf(e.code, e.message);
              // Dòng đã bị xoá trong lúc chờ phản hồi ⇒ không còn chỗ tô, đưa về danh sách lỗi chung.
              if (uid && findItem(itemsRef.current, uid))
                byRow.set(uid, [...(byRow.get(uid) ?? []), text]);
              else details.push(text);
            }
            if (parsed?.truncated)
              details.push('Còn lỗi khác chưa hiển thị — sửa các lỗi trên rồi lưu lại.');
            setRowErrors(byRow);
            setGeneralError({ message: getErrorMessage(code), details });
            return;
          }
          setGeneralError({ message: getErrorMessage(code), details: [] });
        },
      },
    );
  };

  const addCustom = () => {
    if (topLevelFull) return;
    edit([
      ...items,
      { uid: newUid(), key: 'custom', label: '', url: '', hidden: false, children: [] },
    ]);
  };

  const addPage = () => {
    if (topLevelFull || pageToAdd === '') return;
    edit([
      ...items,
      {
        uid: newUid(),
        key: pageKeyOf(pageToAdd),
        label: null,
        url: null,
        hidden: false,
        children: [],
      },
    ]);
    setPageToAdd('');
  };

  const onDragStart = ({ active }: DragStartEvent) => {
    const uid = String(active.id);
    // `drag` khác null ⇒ `flat` bỏ hậu duệ của mục đang kéo: cả nhóm con đi theo mục cha.
    setDrag({ activeUid: uid, overUid: uid, offsetX: 0 });
    setDropError(null);
  };
  const onDragMove = ({ delta }: DragMoveEvent) => {
    setDrag((d) => (d ? { ...d, offsetX: delta.x } : d));
  };
  const onDragOver = ({ over }: DragOverEvent) => {
    setDrag((d) => (d && over ? { ...d, overUid: String(over.id) } : d));
  };
  const onDragEnd = ({ active, over, delta }: DragEndEvent) => {
    setDrag(null);
    if (!over) return;
    const activeUid = String(active.id);
    const overUid = String(over.id);
    const current = removeDescendants(flatten(items), activeUid);
    const proj = getProjection(current, activeUid, overUid, delta.x, INDENT);
    const result = applyMove(items, activeUid, overUid, proj);
    if (!result.ok) {
      setDropError(result.message);
      return;
    }
    edit(result.items);
  };

  const announcements: Announcements = {
    onDragStart: ({ active }) => `Đã nhấc ${describe(active.id)}.`,
    onDragOver: ({ active, over }) =>
      over
        ? `${describe(active.id)} đang ở vị trí của ${describe(over.id)}.`
        : `${describe(active.id)} ngoài danh sách.`,
    onDragEnd: ({ active }) => `Đã thả ${describe(active.id)}.`,
    onDragCancel: ({ active }) => `Huỷ di chuyển ${describe(active.id)}.`,
  };
  function describe(id: UniqueIdentifier): string {
    const entry = flatten(items).find((e) => e.uid === String(id));
    return entry ? nameOf(entry.item) || 'liên kết chưa đặt tên' : 'mục';
  }

  return (
    <div className="space-y-4">
      {notice && (
        <p
          role="alert"
          className="rounded-md border border-border bg-muted px-3 py-2 text-sm text-foreground"
        >
          {notice}
        </p>
      )}
      <p className="text-sm text-muted-foreground">
        Kéo mục để sắp xếp; kéo sang phải để đưa vào nhóm của mục phía trên (tối đa{' '}
        {NAV_LIMITS.levels} cấp). Bằng bàn phím: Tab tới nút kéo, Space để nhấc, mũi tên lên/xuống
        để di chuyển, trái/phải để đổi cấp, Space để thả. Nhãn để trống thì dùng tên trang.
      </p>

      {dropError && (
        <p role="alert" className="text-sm text-destructive">
          {dropError}
        </p>
      )}

      {items.length === 0 ? (
        <p className="rounded-md border border-dashed border-border p-6 text-center text-muted-foreground">
          Menu chưa có mục nào. Thêm trang hoặc liên kết bên dưới.
        </p>
      ) : (
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          measuring={{ droppable: { strategy: MeasuringStrategy.Always } }}
          accessibility={{
            announcements,
            screenReaderInstructions: {
              draggable:
                'Nhấn Space để nhấc mục. Dùng mũi tên lên/xuống để di chuyển, trái/phải để đổi cấp. Nhấn Space để thả, Esc để huỷ.',
            },
          }}
          onDragStart={onDragStart}
          onDragMove={onDragMove}
          onDragOver={onDragOver}
          onDragEnd={onDragEnd}
          onDragCancel={() => {
            setDrag(null);
          }}
        >
          <SortableContext items={flat.map((e) => e.uid)} strategy={verticalListSortingStrategy}>
            <ul aria-label="Các mục menu" className="space-y-2">
              {flat.map((entry, index) => {
                const isActive = drag?.activeUid === entry.uid;
                const depth = isActive && projection ? projection.depth : entry.depth;
                return (
                  <NavRow
                    key={entry.uid}
                    index={index}
                    depth={depth}
                    item={entry.item}
                    name={nameOf(entry.item)}
                    page={
                      isPageKey(entry.item.key)
                        ? pagesById.get(pageIdOf(entry.item.key))
                        : undefined
                    }
                    errors={rowErrors.get(entry.uid)}
                    invalidDrop={isActive && projection !== null && !projection.valid}
                    onChange={(patch) => {
                      edit(updateItem(items, entry.uid, patch), entry.uid);
                    }}
                    onRemove={() => {
                      edit(removeItem(items, entry.uid), entry.uid);
                    }}
                  />
                );
              })}
            </ul>
          </SortableContext>
        </DndContext>
      )}

      <div className="flex flex-wrap items-center gap-2 border-t border-border pt-4">
        <select
          aria-label="Thêm trang vào menu"
          value={pageToAdd}
          onChange={(e) => {
            setPageToAdd(e.target.value);
          }}
          className="rounded-md border border-border bg-background px-2 py-1.5 text-sm"
        >
          <option value="">Chọn trang…</option>
          {addablePages.map((p) => (
            <option key={p.id} value={p.id}>
              {p.title}
            </option>
          ))}
        </select>
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={topLevelFull || pageToAdd === ''}
          onClick={addPage}
        >
          Thêm trang
        </Button>
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={topLevelFull}
          onClick={addCustom}
        >
          Thêm liên kết
        </Button>
      </div>
      {topLevelOver && (
        <p className="text-sm text-destructive">{topLevelOverMessage(items.length)}</p>
      )}
      {topLevelFull && !topLevelOver && (
        <p className="text-sm text-muted-foreground">
          Đã đủ tối đa {NAV_LIMITS.topLevel} mục cấp 1 — muốn thêm thì xoá bớt hoặc kéo mục vào
          trong một nhóm.
        </p>
      )}

      {generalError && (
        <div role="alert" className="space-y-1 text-sm text-destructive">
          <p>{generalError.message}</p>
          {generalError.details.length > 0 && (
            <ul className="list-disc pl-5">
              {generalError.details.map((d, i) => (
                <li key={`${d}-${String(i)}`}>{d}</li>
              ))}
            </ul>
          )}
        </div>
      )}
      <div className="flex items-center gap-3">
        <Button type="button" disabled={saveMutation.isPending} onClick={save}>
          {saveMutation.isPending ? 'Đang lưu...' : 'Lưu menu'}
        </Button>
        {saved && (
          <p role="status" className="text-sm text-muted-foreground">
            Đã lưu menu.
          </p>
        )}
      </div>
    </div>
  );
}

interface RowProps {
  index: number;
  depth: number;
  item: NavItem;
  name: string;
  page: PageDto | undefined;
  errors: string[] | undefined;
  invalidDrop: boolean;
  onChange: (patch: Partial<Pick<NavItem, 'label' | 'url' | 'hidden'>>) => void;
  onRemove: () => void;
}

function NavRow({
  index,
  depth,
  item,
  name,
  page,
  errors,
  invalidDrop,
  onChange,
  onRemove,
}: RowProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({
    id: item.uid,
  });
  const isPage = isPageKey(item.key);
  const invalid = errors !== undefined && errors.length > 0;

  return (
    <li
      ref={setNodeRef}
      data-testid="nav-row"
      data-nav-index={index}
      data-nav-name={name}
      data-invalid={invalid ? 'true' : undefined}
      // Thụt bằng padding của <li> (không phải margin) để rect của mọi dòng cùng mép trái — toạ độ bàn phím tính
      // độ lệch ngang theo cấp, không phụ thuộc cấp đang hiển thị của dòng.
      style={{
        paddingLeft: depth * INDENT,
        transform: transform
          ? `translate3d(${String(transform.x)}px, ${String(transform.y)}px, 0)`
          : undefined,
        transition,
      }}
    >
      <div
        className={[
          'rounded-md border bg-background px-3 py-2',
          invalid || invalidDrop ? 'border-destructive' : 'border-border',
          isDragging ? 'opacity-70 shadow-md' : '',
        ].join(' ')}
      >
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            ref={setActivatorNodeRef}
            {...attributes}
            {...listeners}
            aria-label={`Kéo ${name}`}
            className="cursor-grab rounded px-1 text-muted-foreground hover:bg-muted"
          >
            ⠿
          </button>
          <span className="text-xs text-muted-foreground">Cấp {depth + 1}</span>
          <span className="min-w-24 font-medium text-foreground">{name || 'Liên kết mới'}</span>
          <span className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
            {isPage ? 'Trang' : 'Liên kết'}
          </span>
          {isPage && !page && (
            <span className="rounded bg-destructive/10 px-1.5 py-0.5 text-xs text-destructive">
              Trang không còn tồn tại
            </span>
          )}
          {page && !page.isEnabled && (
            <span className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
              Trang đang tắt
            </span>
          )}
          {item.hidden && (
            <span className="rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
              Đang ẩn
            </span>
          )}
          <div className="ml-auto flex items-center gap-1">
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => {
                onChange({ hidden: !item.hidden });
              }}
            >
              {item.hidden ? 'Hiện' : 'Ẩn'}
            </Button>
            <Button type="button" size="sm" variant="outline" onClick={onRemove}>
              Xoá
            </Button>
          </div>
        </div>
        <div className="mt-2 flex flex-wrap gap-2">
          <input
            type="text"
            aria-label="Nhãn hiển thị"
            value={item.label ?? ''}
            maxLength={NAV_LIMITS.label}
            placeholder={isPage ? (page?.title ?? '') : 'Nhãn liên kết'}
            onChange={(e) => {
              const value = e.target.value;
              onChange({ label: isPage && value === '' ? null : value });
            }}
            className="min-w-40 flex-1 rounded-md border border-border bg-background px-2 py-1 text-sm"
          />
          {!isPage && (
            <input
              type="text"
              aria-label="Địa chỉ liên kết"
              value={item.url ?? ''}
              maxLength={NAV_LIMITS.url}
              placeholder="https://… hoặc /duong-dan"
              onChange={(e) => {
                onChange({ url: e.target.value });
              }}
              className="min-w-56 flex-[2] rounded-md border border-border bg-background px-2 py-1 text-sm"
            />
          )}
        </div>
        {invalid && (
          <ul className="mt-1 space-y-0.5 text-sm text-destructive">
            {errors.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        )}
        {invalidDrop && (
          <p className="mt-1 text-sm text-destructive">
            Không thả được ở đây: menu tối đa {NAV_LIMITS.levels} cấp.
          </p>
        )}
      </div>
    </li>
  );
}

// ── Bàn phím: lên/xuống chọn dòng kế, trái/phải đổi cấp (theo mẫu SortableTree của dnd-kit) ────────────────

/** `event.code` của các phím mũi tên (so chuỗi — `KeyboardCode` của dnd-kit là `enum` TS, #19). */
const KEY = { down: 'ArrowDown', up: 'ArrowUp', left: 'ArrowLeft', right: 'ArrowRight' } as const;
const VERTICAL: string[] = [KEY.down, KEY.up];
const HORIZONTAL: string[] = [KEY.left, KEY.right];

function treeKeyboardCoordinates(
  context: React.RefObject<{ flat: FlatNavItem[]; offsetX: number }>,
): KeyboardCoordinateGetter {
  return (
    event,
    {
      currentCoordinates,
      context: { active, over, collisionRect, droppableRects, droppableContainers },
    },
  ) => {
    if (!VERTICAL.includes(event.code) && !HORIZONTAL.includes(event.code)) return undefined;
    if (!active || !collisionRect) return undefined;
    event.preventDefault();
    const { flat, offsetX } = context.current;

    if (HORIZONTAL.includes(event.code)) {
      if (!over) return undefined;
      const { depth, minDepth, maxDepth } = getProjection(
        flat,
        String(active.id),
        String(over.id),
        offsetX,
        INDENT,
      );
      if (event.code === KEY.left && depth > minDepth) {
        return { ...currentCoordinates, x: currentCoordinates.x - INDENT };
      }
      if (event.code === KEY.right && depth < maxDepth) {
        return { ...currentCoordinates, x: currentCoordinates.x + INDENT };
      }
      return undefined;
    }

    const candidates: DroppableContainer[] = [];
    droppableContainers.forEach((container) => {
      if (container.disabled || container.id === over?.id) return;
      const rect = droppableRects.get(container.id);
      if (!rect) return;
      if (event.code === KEY.down && collisionRect.top < rect.top) candidates.push(container);
      if (event.code === KEY.up && collisionRect.top > rect.top) candidates.push(container);
    });
    const collisions = closestCorners({
      active,
      collisionRect,
      pointerCoordinates: null,
      droppableRects,
      droppableContainers: candidates,
    });
    let closestId = getFirstCollision(collisions, 'id');
    if (closestId === over?.id && collisions.length > 1) closestId = collisions[1]?.id ?? null;
    if (closestId === null || !over) return undefined;

    const newRect = droppableRects.get(closestId);
    const target = flat.find((e) => e.uid === String(closestId));
    const activeEntry = flat.find((e) => e.uid === String(active.id));
    if (!newRect || !target || !activeEntry) return undefined;
    const { depth } = getProjection(
      flat,
      activeEntry.uid,
      target.uid,
      (target.depth - activeEntry.depth) * INDENT,
      INDENT,
    );
    // Mọi dòng cùng mép trái (thụt bằng padding) ⇒ độ lệch ngang so với lúc nhấc = số cấp đổi × INDENT.
    return { x: newRect.left + (depth - activeEntry.depth) * INDENT, y: newRect.top };
  };
}
