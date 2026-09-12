/**
 * Shape của giá trị lưu TRONG TREE cho các kind cần đặc tả riêng (07 §7).
 * Component KHÔNG được tự nối URL — luôn qua ctx.resolveImage()/ctx.resolveUrl() (#11, #53).
 */

/** kind: 'image' — §7.1. `alt` optional, rỗng thì fallback về MediaAsset.AltText (05 §9). */
export interface ImageValue {
  imageId: string;
  alt?: string;
}

/** kind: 'link' — §7.3, discriminated union theo `kind`. */
export type LinkValue =
  | { kind: 'page'; pageId: string }
  | { kind: 'systemPage'; systemType: string }
  | { kind: 'productCategory'; categoryId: number }
  | { kind: 'external'; url: string }
  | { kind: 'anchor'; nodeId: string };
