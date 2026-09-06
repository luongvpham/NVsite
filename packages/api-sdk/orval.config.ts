import { defineConfig } from 'orval';

const header = () => ['GENERATED — DO NOT EDIT (pnpm gen:api)'];

// Input luôn là contract ĐÃ DUYỆT (contracts/openapi/{module}.v{n}.json), không bao giờ .staging/.
export default defineConfig({
  sample: {
    input: '../../contracts/openapi/sample.v1.json',
    output: {
      mode: 'tags-split',
      target: 'src/generated/sample/sample.ts',
      schemas: 'src/generated/sample/model',
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
  sampleZod: {
    input: '../../contracts/openapi/sample.v1.json',
    output: {
      mode: 'single',
      target: 'src/generated/sample/sample.zod.ts',
      client: 'zod',
      override: { header },
    },
  },
});
