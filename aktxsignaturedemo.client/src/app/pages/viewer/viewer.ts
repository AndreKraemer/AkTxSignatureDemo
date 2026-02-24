import { Component, OnInit } from '@angular/core';
import { DocumentService } from '../../services/document.service';
import { DocumentInfo, SignedDocumentInfo } from '../../models/document.models';

@Component({
  selector: 'app-viewer',
  templateUrl: './viewer.html',
  standalone: false,
  styleUrl: './viewer.css',
})
export class ViewerComponent implements OnInit {
  basePath: string;
  documents: DocumentInfo[] = [];
  signedDocuments: SignedDocumentInfo[] = [];
  activeDocument: string | null = null;
  documentData: string = '';
  showViewer = true;
  loadingDoc: string | null = null;
  loadingSignedDoc: string | null = null;

  constructor(private documentService: DocumentService) {
    this.basePath = window.location.origin;
  }

  ngOnInit(): void {
    this.documentService.getDocuments().subscribe({
      next: (docs) => { this.documents = docs; },
    });
    this.refreshSignedDocuments();
  }

  selectDocument(doc: DocumentInfo): void {
    if (this.loadingDoc || this.loadingSignedDoc) return;
    this.loadingDoc = doc.fileName;
    this.documentService.loadForViewer(doc.fileName).subscribe({
      next: (response) => {
        this.documentData = response.data;
        this.activeDocument = doc.fileName;
        this.loadingDoc = null;
        this.showViewer = false;
        setTimeout(() => { this.showViewer = true; });
      },
      error: () => { this.loadingDoc = null; },
    });
  }

  selectSignedDocument(doc: SignedDocumentInfo): void {
    if (this.loadingDoc || this.loadingSignedDoc) return;
    this.loadingSignedDoc = doc.fileName;
    this.documentService.loadSignedForViewer(doc.fileName).subscribe({
      next: (response) => {
        this.documentData = response.data;
        this.activeDocument = doc.fileName;
        this.loadingSignedDoc = null;
        this.showViewer = false;
        setTimeout(() => { this.showViewer = true; });
      },
      error: () => { this.loadingSignedDoc = null; },
    });
  }

  refreshSignedDocuments(): void {
    this.documentService.getSignedDocuments().subscribe({
      next: (docs) => { this.signedDocuments = docs; },
    });
  }

  getSignedDownloadUrl(fileName: string): string {
    return this.documentService.getSignedDownloadUrl(fileName);
  }
}
