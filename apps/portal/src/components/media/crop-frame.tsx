import { type PointerEvent, useState } from 'react';
import { clampCenter, frameSize, presetRatio } from '../../lib/crop-frame';

export interface CropFrameProps {
  src: string;
  alt: string;
  preset: string;
  focal: { x: number; y: number };
  onFocalChange: (focal: { x: number; y: number }) => void;
  /** Preset `inside` (hoặc lạ) → ảnh thuần, không khung, không chọn focal. */
  imageClassName?: string;
}

/**
 * Ảnh + khung đúng tỉ lệ preset. Kéo khung (hoặc bấm vào ảnh) → tâm khung là focal point mới, khung luôn nằm gọn trong ảnh.
 * Chỉ để chọn focal point — server không nhận tham số crop.
 */
export function CropFrame({ src, alt, preset, focal, onFocalChange, imageClassName }: CropFrameProps) {
  const ratio = presetRatio(preset);
  const [aspect, setAspect] = useState<number | null>(null);
  const [dragging, setDragging] = useState(false);

  const frame = ratio !== null && aspect !== null ? frameSize(aspect, ratio) : null;
  const center = frame ? clampCenter(focal.x, focal.y, frame) : focal;

  function pointerToFocal(event: PointerEvent<HTMLDivElement>) {
    if (!frame) return;
    const rect = event.currentTarget.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return;
    onFocalChange(
      clampCenter((event.clientX - rect.left) / rect.width, (event.clientY - rect.top) / rect.height, frame),
    );
  }

  if (ratio === null) {
    return <img src={src} alt={alt} className={imageClassName ?? 'block max-h-64 w-full object-contain'} />;
  }

  return (
    <div className="flex justify-center rounded border border-border bg-muted/30">
      <div
        data-testid="crop-area"
        className="relative inline-block cursor-crosshair touch-none select-none overflow-hidden"
        onPointerDown={(event) => {
          setDragging(true);
          // jsdom không có setPointerCapture; trình duyệt thật có → bắt con trỏ để kéo ra ngoài vùng ảnh vẫn theo.
          // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
          event.currentTarget.setPointerCapture?.(event.pointerId);
          pointerToFocal(event);
        }}
        onPointerMove={(event) => {
          if (dragging) pointerToFocal(event);
        }}
        onPointerUp={() => {
          setDragging(false);
        }}
        onPointerCancel={() => {
          setDragging(false);
        }}
      >
        <img
          src={src}
          alt={alt}
          draggable={false}
          className={imageClassName ?? 'block max-h-64 w-auto max-w-full'}
          onLoad={(event) => {
            const { naturalWidth, naturalHeight } = event.currentTarget;
            if (naturalWidth <= 0 || naturalHeight <= 0) return;
            const nextAspect = naturalWidth / naturalHeight;
            setAspect(nextAspect);
            // Giá trị gửi đi phải trùng khung đang hiện: focal cũ nằm sát mép sẽ bị kéo vào trong ngay khi biết kích thước ảnh.
            const snapped = clampCenter(focal.x, focal.y, frameSize(nextAspect, ratio));
            if (snapped.x !== focal.x || snapped.y !== focal.y) onFocalChange(snapped);
          }}
        />
        {frame && (
          <div
            data-testid="crop-frame"
            className="pointer-events-none absolute border-2 border-white shadow-[0_0_0_9999px_rgba(0,0,0,0.45)]"
            style={{
              width: `${frame.w * 100}%`,
              height: `${frame.h * 100}%`,
              left: `${(center.x - frame.w / 2) * 100}%`,
              top: `${(center.y - frame.h / 2) * 100}%`,
            }}
          />
        )}
      </div>
    </div>
  );
}
