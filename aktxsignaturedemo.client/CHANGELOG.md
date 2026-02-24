This file describes how Visual Studio created the project.

The following tools were used to create this project:
- Angular CLI (ng)

The following steps were used to create this project:
- Created an Angular project with NG: `ng new aktxsignaturedemo.client --defaults --skip-install --skip-git --no-standalone`.
- Added `proxy.conf.js` to proxy calls to the backend ASP.NET server.
- Added `aspnetcore-https.js` script to install HTTPS certificates.
- Updated `package.json` to invoke `aspnetcore-https.js` and serve with HTTPS.
- Updated `angular.json` to point to `proxy.conf.js`.
- Updated app.ts to fetch and display weather information.
- Modified app.spec.ts with updated tests.
- Updated app-module.ts to import HttpClientModule.
- Created project file (`aktxsignaturedemo.client.esproj`).
- Created `launch.json` to enable debugging.
- Created `tasks.json` to enable debugging.
- Updated package.json to add `jest-editor-support`.
- Updated package.json to add `run-script-os`.
- Added `karma.conf.js` for unit tests.
- Updated `angular.json` to point to `karma.conf.js`.
- Added project to solution.
- Updated the proxy endpoint to the backend server endpoint.
- Wrote this file.
