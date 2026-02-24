import { Component, OnDestroy, OnInit, NgZone } from '@angular/core';
import { DocumentService } from '../../services/document.service';
import { DocumentInfo } from '../../models/document.models';

declare const TXTextControl: any;

@Component({
  selector: 'app-editor',
  templateUrl: './editor.html',
  standalone: false,
  styleUrl: './editor.css',
})
export class EditorComponent implements OnInit, OnDestroy {
  webSocketUrl: string;
  backstageOpen = false;
  documents: DocumentInfo[] = [];
  loading = false;
  loadingDoc: string | null = null;
  saving = false;
  connectionID: string | null = null;
  activeDocument: string | null = null;
  private pollTimer: any;

  constructor(
    private documentService: DocumentService,
    private ngZone: NgZone,
  ) {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    this.webSocketUrl = `${protocol}//${window.location.host}/TXWebSocket`;
  }

  ngOnInit(): void {
    this.pollForTextControl();
  }

  ngOnDestroy(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
    }
    if (typeof TXTextControl !== 'undefined') {
      TXTextControl.removeFromDom();
    }
  }

  toggleBackstage(): void {
    this.backstageOpen = !this.backstageOpen;
    if (this.backstageOpen) {
      this.loadDocuments();
    }
  }

  closeBackstage(): void {
    this.backstageOpen = false;
  }

  loadDocuments(): void {
    this.loading = true;
    this.documentService.getDocuments().subscribe({
      next: (docs) => {
        this.documents = docs;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  loadDocument(doc: DocumentInfo): void {
    if (!this.connectionID) return;
    this.loadingDoc = doc.fileName;
    this.documentService.loadIntoEditor(this.connectionID, doc.fileName).subscribe({
      next: () => {
        this.activeDocument = doc.fileName;
        this.loadingDoc = null;
        this.backstageOpen = false;
      },
      error: () => {
        this.loadingDoc = null;
      },
    });
  }

  saveDocument(): void {
    if (!this.connectionID || !this.activeDocument || this.saving) return;
    this.saving = true;
    this.documentService.saveFromEditor(this.connectionID, this.activeDocument).subscribe({
      next: () => {
        this.saving = false;
      },
      error: () => {
        this.saving = false;
      },
    });
  }

  newDocument(): void {
    if (!this.connectionID) return;
    this.loadingDoc = '__new__';
    this.documentService.newDocument(this.connectionID).subscribe({
      next: () => {
        this.activeDocument = null;
        this.loadingDoc = null;
        this.backstageOpen = false;
      },
      error: () => {
        this.loadingDoc = null;
      },
    });
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  private pollForTextControl(): void {
    this.pollTimer = setInterval(() => {
      if (typeof TXTextControl !== 'undefined' && TXTextControl.addEventListener) {
        clearInterval(this.pollTimer);
        this.pollTimer = null;
        TXTextControl.addEventListener('textControlLoaded', () => {
          this.ngZone.run(() => {
            this.connectionID = TXTextControl.connectionID;
          });
        });
        // In case the event already fired
        if (TXTextControl.connectionID) {
          this.ngZone.run(() => {
            this.connectionID = TXTextControl.connectionID;
          });
        }
      }
    }, 200);
  }
}
