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
