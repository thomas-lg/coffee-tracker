# Frontend

Angular 22 PWA — standalone components, signals, Signal Forms, Tailwind, split into
npm workspace packages under `packages/`.

Everything you need is in the repo root:

- **[README.md](../README.md)** — what the app does, how to run it, how to deploy it
- **[CLAUDE.md](../CLAUDE.md)** — architecture rules, comment style, and the gotchas
- **[CONTRIBUTING.md](../CONTRIBUTING.md)** — how to send a change

```bash
npm start      # ng serve app  — dev server on :4200, proxies /api to :5000
npm run build  # ng build app  — production bundle
npm test       # vitest, workspace-wide (every package's specs in one run)
npm run lint
npm run e2e    # Playwright; needs the API on :5000 against an EMPTY database
npm run gen:api  # regenerate the typed client from openapi.json
```

Development happens in the dev container — the host isn't expected to carry Node.
