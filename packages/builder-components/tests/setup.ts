import '@testing-library/jest-dom/vitest';

// Control Lexical được tải lười (import động): lần đầu trong một file test phải transform cả Lexical → vượt 1 s mặc định của findBy*.
import { configure } from '@testing-library/react';
configure({ asyncUtilTimeout: 4000 });
