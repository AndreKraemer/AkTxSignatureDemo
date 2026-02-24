import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DocumentInfo,
  LoadDocumentRequest,
  ViewerDocumentRequest,
  ViewerDocumentResponse,
  SignTokenResponse,
  SignedDocumentInfo,
  ViewerSignedDocumentRequest,
} from '../models/document.models';

@Injectable({ providedIn: 'root' })
export class DocumentService {
  private baseUrl = '/api/documents';

  constructor(private http: HttpClient) {}

  getDocuments(): Observable<DocumentInfo[]> {
    return this.http.get<DocumentInfo[]>(this.baseUrl);
  }

  loadIntoEditor(connectionID: string, fileName: string): Observable<void> {
    const request: LoadDocumentRequest = { connectionID, fileName };
    return this.http.post<void>(`${this.baseUrl}/load`, request);
  }

  saveFromEditor(connectionID: string, fileName: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/save`, { connectionID, fileName });
  }

  newDocument(connectionID: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/new`, { connectionID });
  }

  loadForViewer(fileName: string): Observable<ViewerDocumentResponse> {
    const request: ViewerDocumentRequest = { fileName };
    return this.http.post<ViewerDocumentResponse>(`${this.baseUrl}/viewer`, request);
  }

  getDownloadUrl(fileName: string, format: string = 'pdf'): string {
    return `${this.baseUrl}/download?fileName=${encodeURIComponent(fileName)}&format=${encodeURIComponent(format)}`;
  }

  /** Issues a one-time signature token from the server for the sign-complete endpoint. */
  getSignToken(): Observable<SignTokenResponse> {
    return this.http.post<SignTokenResponse>(`${this.baseUrl}/sign-token`, {});
  }

  /** Returns the list of previously signed documents. */
  getSignedDocuments(): Observable<SignedDocumentInfo[]> {
    return this.http.get<SignedDocumentInfo[]>(`${this.baseUrl}/signed`);
  }

  /** Loads a signed document into the TX Text Control viewer. */
  loadSignedForViewer(fileName: string): Observable<ViewerDocumentResponse> {
    const request: ViewerSignedDocumentRequest = { fileName };
    return this.http.post<ViewerDocumentResponse>(`${this.baseUrl}/signed/viewer`, request);
  }

  getSignedDownloadUrl(fileName: string): string {
    return `${this.baseUrl}/signed/download?fileName=${encodeURIComponent(fileName)}`;
  }
}
