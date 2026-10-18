export * from './mutator/axios-instance';

export * from './generated/identity/identity/identity';
// ProblemDetails/HttpValidationProblemDetails: identity và shop sinh ra bản giống hệt nhau
// (đều tham chiếu components/schemas chung của backend) — chỉ export một bản qua identity/model
// để tránh TS2308 (ambiguous re-export) khi cả hai module cùng export * cùng tên.
export * from './generated/identity/model';
export * from './generated/identity/identity.zod';

export * from './generated/shop/shop/shop';
export * from './generated/shop/model/createShopRequest';
export * from './generated/shop/model/shopDto';
export * from './generated/shop/model/shopKind';
export * from './generated/shop/model/shopStatus';
export * from './generated/shop/model/shopSummaryDto';
export * from './generated/shop/model/updateShopRequest';
export * from './generated/shop/shop.zod';

// media: cùng lý do tránh TS2308 như identity/shop — ProblemDetails/HttpValidationProblemDetails
// (và HttpValidationProblemDetailsErrors) trùng shape với bản identity đã export ở trên, nên chỉ
// export chọn lọc, không export * cả thư mục model.
export * from './generated/media/media/media';
export * from './generated/media/model/cloneRequest';
export * from './generated/media/model/getAssetsByIdsParams';
export * from './generated/media/model/getDerivativesParams';
export * from './generated/media/model/listLibraryParams';
export * from './generated/media/model/iFormFile';
export * from './generated/media/model/mediaAssetDto';
export * from './generated/media/model/mediaReferenceDto';
export * from './generated/media/model/mediaReferenceKind';
export * from './generated/media/model/mediaReferencesDto';
export * from './generated/media/model/mediaUsageDto';
export * from './generated/media/model/pagedResultOfMediaAssetDto';
export * from './generated/media/model/shopLogoDto';
export * from './generated/media/model/slotUploadResultDto';
export * from './generated/media/model/slotUploadResultDtoLibraryAsset';
export * from './generated/media/model/uploadShopLogoForm';
export * from './generated/media/model/uploadToLibraryForm';
export * from './generated/media/model/uploadToSlotForm';
export * from './generated/media/media.zod';

// website: cùng lý do tránh TS2308 — bỏ ProblemDetails/HttpValidationProblemDetails(+Errors) (đã export từ identity).
// `JsonElement` là interface rỗng (tree/tokens): FE kiểu hoá bằng Zod của packages/builder-components.
export * from './generated/website/website/website';
export * from './generated/website/model/createPageRequest';
export * from './generated/website/model/jsonElement';
export * from './generated/website/model/nullableOfSystemType';
export * from './generated/website/model/pageDraftDto';
export * from './generated/website/model/pageDraftPageDto';
export * from './generated/website/model/pageDto';
export * from './generated/website/model/pageKind';
export * from './generated/website/model/savePageDraftRequest';
export * from './generated/website/model/savePageDraftResultDto';
export * from './generated/website/model/themeDto';
export * from './generated/website/model/updatePageRequest';
export * from './generated/website/model/updateThemeRequest';
export * from './generated/website/model/websiteDto';
export * from './generated/website/model/websiteStatus';
export * from './generated/website/website.zod';
