# AK TX Signature Demo

A sample application by André Krämer that demonstrates how to integrate [TX Text Control](https://www.textcontrol.com/) into an Angular + ASP.NET Core application orchestrated with [Aspire](https://aspire.dev) — the open-source, developer-first platform for distributed applications (successor to .NET Aspire). It covers the full document lifecycle: editing, viewing, electronic signing, and cryptographic digital signing with an X.509 certificate.

---

## Table of Contents

1. [What the Application Does](#what-the-application-does)
2. [Tech Stack](#tech-stack)
3. [Project Structure](#project-structure)
4. [Getting Started](#getting-started)
5. [Application Pages](#application-pages)
6. [How PDF Digital Signatures Work with TX Text Control](#how-pdf-digital-signatures-work-with-tx-text-control)
7. [Signature Security: One-Time Tokens](#signature-security-one-time-tokens)
8. [API Reference](#api-reference)
9. [Configuration](#configuration)

---

## What the Application Does

The app walks through three stages of a document workflow:

1. **Edit** — Open a template document in the TX Text Control WYSIWYG editor. Add text, place signature fields, and save back to the server.
2. **View** — Preview a document read-only in the TX Text Control DocumentViewer, with thumbnail navigation and text search.
3. **Sign** — Load a document with signature fields into the DocumentViewer's signature mode. A user can type, draw, or upload their signature and then submit. On submission the server:
   - Applies a cryptographic X.509 digital signature using a PFX certificate.
   - Saves the signed document as both a `.tx` (TX Text Control format) and a `.pdf` file.
   - Returns the signed PDF to the browser for immediate download.

Signed documents can be reviewed at any time in the Document Viewer page.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Orchestration | [Aspire](https://aspire.dev) 13 |
| Backend | ASP.NET Core on .NET 10 |
| Document Processing | TX Text Control v34 |
| Frontend | Angular 19 (NgModule-based) |
| Language | C# 13 / TypeScript 5.8 |
| Testing | Vitest (Angular) |

---

## Project Structure

```
AkTxSignatureDemo.slnx               Solution file
│
├── AkTxSignatureDemo.AppHost/        .NET Aspire orchestrator
│   └── AppHost.cs                    Wires up API + Angular dev server
│
├── AkTxSignatureDemo.Server/         ASP.NET Core Web API
│   ├── App_Data/
│   │   ├── documents/                Template documents (.tx, .docx, .rtf)
│   │   ├── signed-documents/         Signed documents (.tx + .pdf pairs)
│   │   └── signing.pfx               Self-signed X.509 certificate for digital signing
│   ├── Controllers/
│   │   └── DocumentsController.cs    All document API endpoints
│   ├── Filters/
│   │   └── ValidateSignatureTokenAttribute.cs  One-time token security filter
│   ├── Models/
│   │   └── DocumentModels.cs         Request/response DTOs
│   ├── Program.cs                    Service configuration and middleware pipeline
│   └── TXWebServerProcess.cs         Starts the TX Text Control WebSocket server process
│
├── AkTxSignatureDemo.ServiceDefaults/ Shared Aspire defaults (OpenTelemetry, health checks)
│
└── aktxsignaturedemo.client/         Angular 19 frontend
    └── src/app/
        ├── pages/
        │   ├── home/                 Landing page
        │   ├── editor/               Document editor (TX WebSocket editor)
        │   ├── viewer/               Document viewer + signed documents browser
        │   └── sign/                 Signature page
        ├── services/
        │   └── document.service.ts   HTTP client for all document API calls
        └── models/
            └── document.models.ts    TypeScript interfaces matching the C# DTOs
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Aspire](https://aspire.dev) 13: `https://aspire.dev/get-started/install-cli/`
- [Node.js](https://nodejs.org/) 20+
- A valid TX Text Control license (NuGet feed configured in `nuget.config`)

### Run with Aspire

```bash
dotnet run --project AkTxSignatureDemo.AppHost
```

Aspire starts both the .NET API and the Angular dev server, then opens the Aspire dashboard in your browser. Click the Angular app URL to open the application.

### Run backend only

```bash
dotnet run --project AkTxSignatureDemo.Server
```

### Run Angular dev server only

```bash
cd aktxsignaturedemo.client
npm install
npm start
```

---

## Application Pages

### Home (`/`)

Overview of the demo with a description of each page and a guided walkthrough of the signature workflow.

### Document Editor (`/editor`)

Embeds the TX Text Control WYSIWYG editor via a WebSocket connection to the .NET backend. You can:
- Open one of the built-in sample documents (Welcome Letter, Service Agreement, NDA).
- Edit the document content.
- Add signature fields using the TX Text Control toolbar.
- Save the document back to the server.

The editor communicates over WebSocket — TX Text Control's `TXWebSocketMiddleware` on the server side handles the protocol.

### Document Viewer (`/viewer`)

Embeds the TX Text Control DocumentViewer. Features:
- Thumbnail pane for multi-page navigation.
- Text selection and search.
- Load any template from `App_Data/documents`.
- Browse and reload previously signed documents stored in `App_Data/signed-documents`.

### Sign Document (`/sign`)

Embeds the DocumentViewer in signature mode. When you select a document:
1. The Angular client requests a one-time signature token from the server.
2. The document data and the token are fetched in parallel (`forkJoin`).
3. The token is embedded in the `redirectUrlAfterSignature` callback URL.
4. You fill in the signature fields (type, draw, or upload your signature).
5. Clicking "Submit" in the TX Text Control signature bar triggers a server-side POST to `/api/documents/sign-complete?signatureToken=...`.
6. The server validates and consumes the token, applies a digital X.509 signature, saves `.tx` + `.pdf` copies, and returns the signed PDF as Base64.
7. Your browser downloads the signed PDF automatically.

---

## How PDF Digital Signatures Work with TX Text Control

### Electronic vs. Digital Signatures

| | Electronic Signature | Digital Signature |
|---|---|---|
| **What it is** | A visual mark (drawn, typed, or uploaded image) placed in a signature field | A cryptographic hash embedded in the PDF, tied to an X.509 certificate |
| **Who creates it** | The end user in the browser | The server using a private key from a PFX certificate |
| **What it proves** | Intent to sign | Identity of the signer + document integrity (the PDF has not been changed since signing) |
| **How it looks** | Visible in the document | Also visible, but the key value is in the PDF's digital signature dictionary |

TX Text Control handles **both** in this application:

- The user applies an **electronic signature** interactively via the DocumentViewer's signature bar.
- The server then applies a **digital signature** using `TXTextControl.DigitalSignature` and `X509Certificate2`, embedding a cryptographic signature into the output PDF.

### The Signing Flow in Detail

```
Browser (Angular)                  Server (ASP.NET Core)
─────────────────                  ─────────────────────
1. POST /sign-token          →     Generate token, store in IMemoryCache (15 min TTL)
                             ←     { token, expiresAt }

2. POST /viewer (forkJoin)   →     Load document as Base64 IUF
                             ←     { data: "<base64>" }

3. User fills signature fields in the DocumentViewer

4. TX middleware POSTs       →     ValidateSignatureTokenAttribute consumes token
   sign-complete?signatureToken=…  Load IUF into ServerTextControl
                                   Apply X509Certificate2 from signing.pfx
                                   Save .tx (electronic sig preserved)
                                   Save .pdf (digital sig embedded)
                             ←     Base64 PDF

5. setSubmitCallback fires         Browser downloads the PDF
```

### X.509 Certificate and PFX

A **PFX** (`.pfx` / `.p12`) file bundles an X.509 certificate together with its private key in a password-protected PKCS#12 container. In this demo a self-signed certificate is used:

- Certificate subject: `CN=AkTxSignatureDemo, O=Andre Kraemer Demo`
- Key algorithm: RSA 2048-bit
- Validity: 10 years
- Location: `AkTxSignatureDemo.Server/App_Data/signing.pfx`
- Password: configured in `appsettings.json` under `SignatureSettings:PfxPassword`

In production you would replace this with a certificate issued by a trusted Certificate Authority (CA) so that PDF viewers show the signature as "trusted".

### TX Text Control Digital Signature API

The relevant TX Text Control code in `DocumentsController.cs`:

```csharp
var cert = new X509Certificate2(pfxPath, pfxPassword);
var saveSettings = new TXTextControl.SaveSettings
{
    SignatureFields = new TXTextControl.DigitalSignature[]
    {
        new TXTextControl.DigitalSignature(cert, null, "txsign")
    }
};
sc.Save(out pdfData, TXTextControl.BinaryStreamType.AdobePDF, saveSettings);
```

The third argument to `DigitalSignature` (`"txsign"`) is the name of the signature field in the document that should receive the digital signature. This links the visual electronic signature the user drew with the cryptographic digital signature in the PDF output.

---

## Signature Security: One-Time Tokens

The `sign-complete` endpoint is protected by `ValidateSignatureTokenAttribute`, an ASP.NET Core `ActionFilterAttribute` that:

1. Reads the `signatureToken` query parameter from the incoming request.
2. Looks up the token in `IMemoryCache` (key pattern: `sig_token:{token}`).
3. If valid, **removes** it immediately — the token can only be used once.
4. If missing or expired, returns HTTP 401.

This prevents replay attacks: even if someone intercepts the callback URL, the token has already been consumed by the time the legitimate request completes.

Token lifetime is configurable via `SignatureSettings:TokenExpirationMinutes` (default: 15 minutes).

---

## API Reference

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/documents` | List template documents |
| `POST` | `/api/documents/load` | Load document into editor (WebSocket session) |
| `POST` | `/api/documents/viewer` | Load template for document viewer |
| `POST` | `/api/documents/save` | Save editor content to file |
| `POST` | `/api/documents/new` | Create empty document in editor |
| `GET` | `/api/documents/download` | Download template as PDF or DOCX |
| `POST` | `/api/documents/sign-token` | Issue a one-time signature token |
| `POST` | `/api/documents/sign-complete` | Process signed document (protected by token) |
| `GET` | `/api/documents/signed` | List signed documents |
| `GET` | `/api/documents/signed/download` | Download a signed PDF |
| `POST` | `/api/documents/signed/viewer` | Load signed document for viewer |

---

## Configuration

`AkTxSignatureDemo.Server/appsettings.json`:

```json
{
  "SignatureSettings": {
    "PfxPath": "App_Data/signing.pfx",
    "PfxPassword": "AkTxSign2024!",
    "SignedDocumentsPath": "App_Data/signed-documents",
    "TokenExpirationMinutes": 15
  }
}
```

> **Note:** In production, move the PFX password out of `appsettings.json` and into an environment variable or a secrets manager (e.g., Azure Key Vault, .NET User Secrets).

---

*Sample application by André Krämer · Built with TX Text Control v34 and [Aspire](https://aspire.dev) 13*
