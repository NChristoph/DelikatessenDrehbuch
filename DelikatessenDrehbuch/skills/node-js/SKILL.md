---
name: node-js
description: Build, debug, refactor, and maintain Node.js applications in JavaScript or TypeScript. Use when handling Node.js backends (Express/Fastify/Nest), CLI tools, package management (npm/pnpm/yarn), test and lint setup, module-system changes (CommonJS or ESM), runtime diagnostics, or dependency upgrades.
---

# Node.js

Use this workflow to deliver reliable Node.js changes with minimal iteration.

## 1) Baseline project context

- Read `package.json`, lockfiles, and `tsconfig.json` (if present).
- Detect package manager from lockfiles:
  - `pnpm-lock.yaml` -> `pnpm`
  - `yarn.lock` -> `yarn`
  - `package-lock.json` -> `npm`
- Check engine constraints in `package.json` (`engines.node`).
- Confirm module mode (`type: "module"` vs CommonJS usage).

## 2) Pick the smallest safe change

- Prefer targeted edits over broad rewrites.
- Keep public API contracts and script names stable unless requested.
- Preserve existing framework conventions and folder layout.
- Add dependencies only when necessary and justify them in the response.

## 3) Implement and verify

- Update code and related tests together.
- Run the narrowest relevant validation first, then broader checks:
  - single test file
  - package test target
  - lint/typecheck for touched code
- If commands fail, surface root cause and propose a minimal fix path.

## 4) Node.js debugging checklist

- Startup crashes: inspect import paths, env variables, and module mode mismatches.
- Async issues: verify missing `await`, unhandled rejections, and promise chains.
- HTTP/API issues: validate request schema handling, status codes, and error middleware.
- Performance issues: inspect hot paths, accidental sync I/O, and N+1 request patterns.

## 5) Delivery format

- Report what changed, where, and why.
- List executed commands and results.
- Call out any skipped verification and remaining risk.