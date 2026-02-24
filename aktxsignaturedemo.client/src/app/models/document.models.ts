export interface DocumentInfo {
  fileName: string;
  displayName: string;
  extension: string;
  sizeBytes: number;
  lastModified: string;
}

export interface LoadDocumentRequest {
  connectionID: string;
  fileName: string;
}

export interface ViewerDocumentRequest {
  fileName: string;
}

export interface ViewerDocumentResponse {
  data: string;
}

export interface SignTokenResponse {
  token: string;
  expiresAt: string;
}

export interface SignedDocumentInfo {
  fileName: string;
  displayName: string;
  extension: string;
  signedAt: string;
  sizeBytes: number;
}

export interface ViewerSignedDocumentRequest {
  fileName: string;
}
