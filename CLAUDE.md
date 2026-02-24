# AkTxSignatureDemo

## Project Overview
.NET Aspire solution with an Angular frontend and ASP.NET Core backend, integrating TX Text Control for document editing/viewing.

## Architecture

### Solution Structure
- **AkTxSignatureDemo.AppHost** — .NET Aspire orchestrator (`AppHost.cs`). Wires up the API server and Angular dev server.
- **AkTxSignatureDemo.Server** — ASP.NET Core Web API (net10.0). Hosts controllers, TX Text Control WebSocket/DocumentViewer middleware, and serves the Angular SPA in production.
- **AkTxSignatureDemo.ServiceDefaults** — Shared Aspire service defaults (OpenTelemetry, health checks, service discovery, resilience).
- **aktxsignaturedemo.client** — Angular 19 frontend (NgModule-based, not standalone). Uses Vitest for testing.

### Key Technologies
- **.NET 10** / **Aspire 13.1.1**
- **Angular 19** with TypeScript 5.8
- **TX Text Control** (v34) — document editor & viewer via WebSocket middleware, with Angular packages `@txtextcontrol/tx-ng-document-editor` and `@txtextcontrol/tx-ng-document-viewer`
- **Vitest** (not Karma/Jest) for Angular unit tests

### Backend Entry Point
`AkTxSignatureDemo.Server/Program.cs` — configures controllers, CORS, OpenAPI, WebSockets (TX Text Control), and DocumentViewer middleware.

### Frontend Entry Point
`aktxsignaturedemo.client/src/main.ts` — bootstraps `AppModule` via `platformBrowser()`.

## Commands

### Build & Run (Aspire)
```
dotnet run --project AkTxSignatureDemo.AppHost
```

### Build Backend Only
```
dotnet build AkTxSignatureDemo.Server
```

### Frontend Dev
```
cd aktxsignaturedemo.client
npm start
```

### Frontend Tests
```
cd aktxsignaturedemo.client
npm test
```

## Conventions
- Angular uses **NgModule** pattern (not standalone components)
- API proxy: Angular dev server proxies `/weatherforecast` to the .NET backend
- CORS is configured to allow all origins in development
- NuGet credentials for TX Text Control are in `nuget.config` (git-ignored)
