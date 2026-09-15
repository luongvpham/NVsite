import { defineConfig } from 'orval';

const header = () => ['GENERATED — DO NOT EDIT (pnpm gen:api)'];

// Input luôn là contract ĐÃ DUYỆT (contracts/openapi/{module}.v{n}.json), không bao giờ .staging/.
export default defineConfig({
  identity: {
    input: '../../contracts/openapi/identity.v1.json',
    output: {
      mode: 'tags-split',
      target: 'src/generated/identity/identity.ts',
      schemas: 'src/generated/identity/model',
      client: 'react-query',
      mock: { type: 'msw' },
      override: {
        mutator: {
          path: './src/mutator/axios-instance.ts',
          name: 'customInstance',
        },
        header,
      },
    },
  },
  identityZod: {
    input: '../../contracts/openapi/identity.v1.json',
    output: {
      mode: 'single',
      target: 'src/generated/identity/identity.zod.ts',
      client: 'zod',
      override: { header },
    },
  },
  shop: {
    input: '../../contracts/openapi/shop.v1.json',
    output: {
      mode: 'tags-split',
      target: 'src/generated/shop/shop.ts',
      schemas: 'src/generated/shop/model',
      client: 'react-query',
      mock: { type: 'msw' },
      override: {
        mutator: {
          path: './src/mutator/axios-instance.ts',
          name: 'customInstance',
        },
        header,
      },
    },
  },
  shopZod: {
    input: '../../contracts/openapi/shop.v1.json',
    output: {
      mode: 'single',
      target: 'src/generated/shop/shop.zod.ts',
      client: 'zod',
      override: { header },
    },
  },
});
