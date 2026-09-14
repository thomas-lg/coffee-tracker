// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = tseslint.config(
  {
    // Build outputs, generated code, and tooling scripts aren't linted.
    ignores: [
      'dist/**',
      'out-tsc/**',
      '.angular/**',
      'coverage/**',
      'playwright-report/**',
      'packages/data/src/lib/models/api-types.ts',
      '**/*.mjs',
      // Not covered by any tsconfig, so the type-aware parser cannot resolve them.
      'playwright.config.ts',
      'eslint.config.js',
    ],
  },
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      // Type-aware. The rules worth having in a codebase this strict, floating
      // promises, misused promises, unsafe `any` flowing out of untyped APIs, all
      // need the type checker. projectService resolves each file's owning tsconfig.
      ...tseslint.configs.recommendedTypeChecked,
      ...angular.configs.tsRecommended,
    ],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: __dirname,
      },
    },
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: 'ct', style: 'camelCase' },
      ],
      // One prefix. `app` was the CLI's default and only ever applied to the root
      // component; everything written since is `ct`.
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'ct', style: 'kebab-case' },
      ],
      // Underscore-prefixed names are intentionally unused (e.g. the compile-time
      // type-parity guards in data/models.ts).
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_', caughtErrorsIgnorePattern: '^_' },
      ],
    },
  },
  {
    // Specs drive Angular test APIs that are typed `any` at the boundary
    // (fixture.componentInstance, JSON round-trips through localStorage) and dispatch
    // rxMethods that return void. Source code carries the type-aware rules; asserting
    // them here would mean casting at every call into the framework, which buys noise
    // rather than safety.
    files: ['**/*.spec.ts'],
    rules: {
      '@typescript-eslint/no-unsafe-assignment': 'off',
      '@typescript-eslint/no-unsafe-member-access': 'off',
      '@typescript-eslint/no-unsafe-call': 'off',
      '@typescript-eslint/no-unsafe-argument': 'off',
      '@typescript-eslint/await-thenable': 'off',
    },
  },
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {
      // `x != null` is the intentional "neither null nor undefined" check in templates.
      '@angular-eslint/template/eqeqeq': ['error', { allowNullOrUndefined: true }],
    },
  },
);
