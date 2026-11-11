import type { ComponentManifest } from '../meta/manifest-schema';
import gallery from '../registry/gallery.manifest';
import hero from '../registry/hero.manifest';
import richText from '../registry/rich-text.manifest';
import section from '../registry/section.manifest';
import serviceGrid from '../registry/service-grid.manifest';

/**
 * Manifest của mọi component — nguồn nhãn tiếng Việt (type + variant) cho palette editor. Chỉ ĐỌC, không sửa
 * manifest (sửa = đổi contract Registry, cần người duyệt).
 *
 * Danh sách này viết tay vì `registry/` không sinh ra artifact chứa nhãn. Thêm component mới mà quên thêm vào đây thì
 * `tests/manifests.test.ts` fail (so với `generated/tree-rules.json`).
 */
export const componentManifests: readonly ComponentManifest[] = [gallery, hero, richText, section, serviceGrid];
