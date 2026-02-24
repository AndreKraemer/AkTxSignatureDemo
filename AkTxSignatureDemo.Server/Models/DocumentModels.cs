namespace AkTxSignatureDemo.Server.Models;

/// <summary>
/// Metadata describing a document file available on the server.
/// </summary>
public class DocumentInfo
{
    /// <summary>Gets or sets the file name including extension (e.g., "contract.docx").</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets a human-readable display name without the file extension.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the file extension including the leading dot (e.g., ".docx").</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>Gets or sets the size of the file in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Gets or sets the UTC date and time when the file was last modified.</summary>
    public DateTime LastModified { get; set; }
}

/// <summary>
/// Request payload for loading a document into the TX Text Control editor via WebSocket.
/// </summary>
public class LoadDocumentRequest
{
    /// <summary>Gets or sets the WebSocket connection ID that identifies the active editor session.</summary>
    public string ConnectionID { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the document file to load (relative to App_Data/documents).</summary>
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Request payload for loading a document into the TX Text Control document viewer.
/// </summary>
public class ViewerDocumentRequest
{
    /// <summary>Gets or sets the name of the document file to load (relative to App_Data/documents).</summary>
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Response containing the Base64-encoded document data for the TX Text Control viewer.
/// </summary>
public class ViewerDocumentResponse
{
    /// <summary>Gets or sets the Base64-encoded document content.</summary>
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Request payload for saving a document from the TX Text Control editor back to the server.
/// </summary>
public class SaveDocumentRequest
{
    /// <summary>Gets or sets the WebSocket connection ID that identifies the active editor session.</summary>
    public string ConnectionID { get; set; } = string.Empty;

    /// <summary>Gets or sets the target file name (relative to App_Data/documents) under which to save the document.</summary>
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Response returned by the sign-token endpoint. Contains a short-lived, one-time token
/// that must be passed as a query parameter to the sign-complete endpoint.
/// </summary>
public class SignTokenResponse
{
    /// <summary>Gets or sets the one-time signature token.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC date and time at which the token expires.</summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Metadata describing a signed document stored in the App_Data/signed-documents folder.
/// </summary>
public class SignedDocumentInfo
{
    /// <summary>Gets or sets the file name including extension (e.g., "contract_20240101_120000.pdf").</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name derived from the original template name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the file extension including the leading dot.</summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp at which the document was signed.</summary>
    public DateTime SignedAt { get; set; }

    /// <summary>Gets or sets the size of the file in bytes.</summary>
    public long SizeBytes { get; set; }
}

/// <summary>
/// Request payload for loading a signed document into the TX Text Control document viewer.
/// </summary>
public class ViewerSignedDocumentRequest
{
    /// <summary>Gets or sets the file name of the signed document (relative to App_Data/signed-documents).</summary>
    public string FileName { get; set; } = string.Empty;
}
