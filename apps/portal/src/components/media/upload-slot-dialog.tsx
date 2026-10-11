import { type MouseEvent, useState } from 'react';
import { Button, Dialog } from '@vsite/ui';
import { getErrorCode } from '@vsite/shared';
import { useUploadToSlot, type MediaAssetDto } from '@vsite/api-sdk';
import { getErrorMessage, HEIC_UNSUPPORTED_GUIDANCE } from '../../lib/error-messages';
import { ACCEPT_ATTR, fileSchema, fitOfPreset, isHeicFile } from '../../lib/media-validation';

export interface UploadSlotDialogProps {
  shopId: string;
  /** Preset của slot đang chỉnh — quyết định tỉ lệ crop và có hiện focal point picker hay không. */
  preset: string;
  open: boolean;
  onClose: () => void;
  /** `asset` (SlotUploadResultDto.asset) — record đặt vào tree, KHÔNG phải `libraryAsset` (#71). */
  onUploaded: (asset: MediaAssetDto) => void;
}

/**
 * F2 — dialog upload ảnh vào một slot của component (Hero/Gallery...).
 * Client kiểm ≤10MB + mime trước khi gửi; ẩn focal point khi preset `fit=inside` (brief F2).
 */
export function UploadSlotDialog({ shopId, preset, open, onClose, onUploaded }: UploadSlotDialogProps) {
  const [file, setFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [clientError, setClientError] = useState<string | null>(null);
  const [clientHeic, setClientHeic] = useState(false);
  const [focalX, setFocalX] = useState(0.5);
  const [focalY, setFocalY] = useState(0.5);
  const [saveToLibrary, setSaveToLibrary] = useState(false);
  const [altText, setAltText] = useState('');

  const showFocalPicker = fitOfPreset(preset) !== 'inside';

  const uploadMutation = useUploadToSlot({
    mutation: {
      onSuccess: (result) => {
        onUploaded(result.asset);
        resetAndClose();
      },
    },
  });

  function resetAndClose() {
    setFile(null);
    setPreviewUrl(null);
    setClientError(null);
    setFocalX(0.5);
    setFocalY(0.5);
    setSaveToLibrary(false);
    setAltText('');
    uploadMutation.reset();
    onClose();
  }

  function handleFileChange(event: React.ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0] ?? null;
    setClientError(null);
    setClientHeic(false);
    if (!selected) {
      setFile(null);
      setPreviewUrl(null);
      return;
    }
    const result = fileSchema.safeParse(selected);
    if (!result.success) {
      setClientError(result.error.issues[0]?.message ?? 'Ảnh không hợp lệ');
      setClientHeic(isHeicFile(selected));
      setFile(null);
      setPreviewUrl(null);
      return;
    }
    setFile(selected);
    setPreviewUrl(URL.createObjectURL(selected));
  }

  function handlePreviewClick(event: MouseEvent<HTMLDivElement>) {
    if (!showFocalPicker) return;
    const rect = event.currentTarget.getBoundingClientRect();
    const x = (event.clientX - rect.left) / rect.width;
    const y = (event.clientY - rect.top) / rect.height;
    setFocalX(Math.min(1, Math.max(0, x)));
    setFocalY(Math.min(1, Math.max(0, y)));
  }

  function handleSubmit() {
    if (!file) {
      setClientError('Vui lòng chọn một ảnh');
      return;
    }
    uploadMutation.mutate({
      shopId,
      data: {
        file,
        preset,
        focalX: showFocalPicker ? focalX : undefined,
        focalY: showFocalPicker ? focalY : undefined,
        saveToLibrary,
        altText: altText.trim().length > 0 ? altText.trim() : undefined,
      },
    });
  }

  const errorCode = getErrorCode(uploadMutation.error);
  const isHeicError = errorCode === 'MEDIA_HEIC_UNSUPPORTED';

  return (
    <Dialog open={open} onClose={resetAndClose} title="Tải ảnh lên">
      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium" htmlFor="slot-upload-file">
            Chọn ảnh
          </label>
          <input
            id="slot-upload-file"
            type="file"
            accept={ACCEPT_ATTR}
            onChange={handleFileChange}
            className="mt-1 block w-full text-sm"
          />
        </div>

        {clientError && (
          <div className="text-sm text-destructive" role="alert">
            <p>{clientError}</p>
            {clientHeic && <p className="mt-1">{HEIC_UNSUPPORTED_GUIDANCE}</p>}
          </div>
        )}

        {previewUrl && (
          <div>
            <div
              className="relative cursor-crosshair overflow-hidden rounded border border-border"
              onClick={handlePreviewClick}
            >
              <img src={previewUrl} alt="Xem trước" className="block max-h-64 w-full object-contain" />
              {showFocalPicker && (
                <div
                  className="pointer-events-none absolute h-3 w-3 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white bg-primary"
                  style={{ left: `${focalX * 100}%`, top: `${focalY * 100}%` }}
                />
              )}
            </div>
            {showFocalPicker ? (
              <p className="mt-1 text-xs text-muted-foreground">Bấm vào ảnh để đặt điểm lấy nét (focal point).</p>
            ) : (
              <p className="mt-1 text-xs text-muted-foreground">Preset này giữ nguyên tỉ lệ ảnh, không cần điểm lấy nét.</p>
            )}
          </div>
        )}

        <div>
          <label className="block text-sm font-medium" htmlFor="slot-upload-alt">
            Mô tả ảnh (alt text, tuỳ chọn)
          </label>
          <input
            id="slot-upload-alt"
            type="text"
            value={altText}
            onChange={(event) => { setAltText(event.target.value); }}
            className="mt-1 w-full rounded-md border border-border px-3 py-2"
          />
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={saveToLibrary}
            onChange={(event) => { setSaveToLibrary(event.target.checked); }}
          />
          Lưu vào thư viện
        </label>

        {uploadMutation.isError && (
          <div role="alert" className="rounded border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
            <p>{getErrorMessage(errorCode)}</p>
            {isHeicError && <p className="mt-1">{HEIC_UNSUPPORTED_GUIDANCE}</p>}
          </div>
        )}

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={resetAndClose}>
            Huỷ
          </Button>
          <Button type="button" onClick={handleSubmit} disabled={uploadMutation.isPending}>
            {uploadMutation.isPending ? 'Đang tải lên...' : 'Tải lên'}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
