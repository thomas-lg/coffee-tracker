// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

/**
 * Each package may import the *public* surface of the packages below it, and nothing else.
 * Anything absent from a list is an error, so adding an edge is a deliberate edit here.
 */
const MAY_IMPORT = {
  util: [],
  data: [],
  ui: ['util'],
  auth: ['ui', 'util', 'data'],
  coffees: ['auth', 'ui', 'util', 'data'],
  admin: ['coffees', 'auth', 'ui', 'util', 'data'],
  app: ['admin', 'coffees', 'auth', 'ui', 'util', 'data'],
};

/** Packages that route: only these have a `pages/` to protect from their `components/`. */
const HAS_PAGES = ['app', 'auth', 'coffees', 'admin'];

const PACKAGES = Object.keys(MAY_IMPORT);

function restrictedImports(pkg, extra = []) {
  const foreign = PACKAGES.filter((p) => p !== pkg && !MAY_IMPORT[pkg].includes(p));
  return {
    '@typescript-eslint/no-restricted-imports': [
      'error',
      {
        patterns: [
          {
            group: [...foreign.map((p) => `@coffee-tracker/${p}`), `@coffee-tracker/${pkg}`],
            message:
              `packages/${pkg} may import only ${MAY_IMPORT[pkg].join(', ') || 'nothing'}. ` +
              'Its own package is reached through the @' +
              pkg +
              '/ alias, not through its own barrel.',
          },
          {
            group: PACKAGES.filter((p) => p !== pkg).map((p) => `@${p}/*`),
            message:
              "The @x/ aliases are one package's own inside. Another package is reached through " +
              'its @coffee-tracker/x barrel, which is what decides what it exposes.',
          },
          {
            group: ['**/packages/*/src/**'],
            message:
              'A relative path into another package walks past its public-api.ts. Import the ' +
              '@coffee-tracker/x barrel instead.',
          },
          ...extra,
        ],
      },
    ],
  };
}

/**
 * Pages compose components. The reverse makes the two indistinguishable again, which is the
 * thing the split exists to prevent.
 */
const NO_PAGE_FROM_COMPONENT = {
  group: ['**/pages/**', '@*/pages/*'],
  message: 'A component must not import a page; pages compose components, never the reverse.',
};


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
  ...PACKAGES.map((pkg) => ({
    files: [`packages/${pkg}/**/*.ts`],
    rules: restrictedImports(pkg),
  })),
  // Repeats each package's patterns rather than adding to them: in flat config a later block
  // setting the same rule replaces it outright for the files it matches.
  ...HAS_PAGES.map((pkg) => ({
    files: [`packages/${pkg}/**/components/**/*.ts`],
    rules: restrictedImports(pkg, [NO_PAGE_FROM_COMPONENT]),
  })),
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {
      // `x != null` is the intentional "neither null nor undefined" check in templates.
      '@angular-eslint/template/eqeqeq': ['error', { allowNullOrUndefined: true }],
    },
  },
);
