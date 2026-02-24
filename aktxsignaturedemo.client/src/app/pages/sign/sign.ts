import { Component, OnInit, OnDestroy } from '@angular/core';
import { forkJoin } from 'rxjs';
import { DocumentService } from '../../services/document.service';
import { DocumentInfo } from '../../models/document.models';

declare const TXDocumentViewer: any;

@Component({
  selector: 'app-sign',
  templateUrl: './sign.html',
  standalone: false,
  styleUrl: './sign.css',
})
export class SignComponent implements OnInit, OnDestroy {
  basePath: string;
  signatureSettings: any = null;
  documents: DocumentInfo[] = [];
  activeDocument: string | null = null;
  documentData: string = '';
  showViewer = true;
  loadingDoc: string | null = null;

  private viewerLoadedHandler = this.onViewerLoaded.bind(this);

  constructor(private documentService: DocumentService) {
    this.basePath = window.location.origin;
  }

  ngOnInit(): void {
    window.addEventListener('documentViewerLoaded', this.viewerLoadedHandler);

    this.documentService.getDocuments().subscribe({
      next: (docs) => { this.documents = docs; },
    });
  }

  ngOnDestroy(): void {
    window.removeEventListener('documentViewerLoaded', this.viewerLoadedHandler);
  }

  selectDocument(doc: DocumentInfo): void {
    if (this.loadingDoc) return;
    this.loadingDoc = doc.fileName;

    // Fetch a fresh one-time token and the document data in parallel
    forkJoin({
      tokenResp: this.documentService.getSignToken(),
      viewerResp: this.documentService.loadForViewer(doc.fileName),
    }).subscribe({
      next: ({ tokenResp, viewerResp }) => {
        this.documentData = viewerResp.data;
        this.activeDocument = doc.fileName;
        this.signatureSettings = this.buildSignatureSettings(tokenResp.token, doc.fileName);
        this.loadingDoc = null;
        // Toggle viewer to force re-initialization with new data + signature settings
        this.showViewer = false;
        setTimeout(() => { this.showViewer = true; });
      },
      error: () => { this.loadingDoc = null; },
    });
  }

  private onViewerLoaded(): void {
    if (typeof TXDocumentViewer !== 'undefined' && TXDocumentViewer.signatures) {
      TXDocumentViewer.signatures.setSubmitCallback((base64Pdf: string) => {
        const link = document.createElement('a');
        link.href = 'data:application/pdf;base64,' + base64Pdf;
        link.download = this.activeDocument
          ? this.activeDocument.replace(/\.[^.]+$/, '') + '_signed.pdf'
          : 'signed_document.pdf';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
      });
    }
  }

  private buildSignatureSettings(token: string, sourceFileName: string): any {
    const sourceName = sourceFileName.replace(/\.[^.]+$/, '');
    const redirectUrl =
      `${this.basePath}/api/documents/sign-complete` +
      `?signatureToken=${encodeURIComponent(token)}` +
      `&sourceName=${encodeURIComponent(sourceName)}`;

    return {
      ownerName: 'André Krämer',
      signerName: 'John Doe',
      signerInitials: 'JD',
      showSignatureBar: true,
      uniqueId: crypto.randomUUID(),
      redirectUrlAfterSignature: redirectUrl,
      signatureBoxes: [
        { name: 'txsign', signingRequired: true, style: 0 },
        { name: 'txinitials', signingRequired: false, style: 1 },
      ],
    };
  }
}
