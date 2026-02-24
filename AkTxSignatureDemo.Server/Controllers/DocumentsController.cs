using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using AkTxSignatureDemo.Server.Filters;
using AkTxSignatureDemo.Server.Models;
using TXTextControl.Web;
using System.Security.Cryptography.X509Certificates;

namespace AkTxSignatureDemo.Server.Controllers;

/// <summary>
/// API controller for all document operations: listing, editing, viewing, signing, and managing signed documents.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private static readonly string[] SupportedExtensions = [".tx", ".docx", ".rtf", ".txt", ".pdf"];

    private readonly string _documentsPath;
    private readonly string _signedDocumentsPath;
    private readonly string _pfxPath;
    private readonly string _pfxPassword;
    private readonly int _tokenExpirationMinutes;
    private readonly ILogger<DocumentsController> _logger;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Initializes a new instance of <see cref="DocumentsController"/>.
    /// </summary>
    /// <param name="env">The web host environment, used to resolve the content root path.</param>
    /// <param name="configuration">The application configuration, used to read <c>SignatureSettings</c>.</param>
    /// <param name="cache">The in-memory cache used for one-time signature token storage.</param>
    /// <param name="logger">The logger for this controller.</param>
    public DocumentsController(
        IWebHostEnvironment env,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<DocumentsController> logger)
    {
        _logger = logger;
        _cache = cache;
        _documentsPath = Path.Combine(env.ContentRootPath, "App_Data", "documents");

        var sigSettings = configuration.GetSection("SignatureSettings");
        var signedRelPath = sigSettings["SignedDocumentsPath"] ?? "App_Data/signed-documents";
        _signedDocumentsPath = Path.Combine(env.ContentRootPath, signedRelPath.Replace('/', Path.DirectorySeparatorChar));

        var pfxRelPath = sigSettings["PfxPath"] ?? "App_Data/signing.pfx";
        _pfxPath = Path.Combine(env.ContentRootPath, pfxRelPath.Replace('/', Path.DirectorySeparatorChar));
        _pfxPassword = sigSettings["PfxPassword"] ?? string.Empty;
        _tokenExpirationMinutes = sigSettings.GetValue<int>("TokenExpirationMinutes", 15);

        Directory.CreateDirectory(_documentsPath);
        Directory.CreateDirectory(_signedDocumentsPath);
        EnsureSampleDocuments();
    }

    // ─── Template documents ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the list of template documents available in the <c>App_Data/documents</c> folder.
    /// </summary>
    /// <returns>An ordered list of <see cref="DocumentInfo"/> objects.</returns>
    [HttpGet]
    public ActionResult<IEnumerable<DocumentInfo>> List()
    {
        if (!Directory.Exists(_documentsPath))
            return Ok(Array.Empty<DocumentInfo>());

        var files = Directory.GetFiles(_documentsPath)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(f =>
            {
                var info = new FileInfo(f);
                return new DocumentInfo
                {
                    FileName = info.Name,
                    DisplayName = Path.GetFileNameWithoutExtension(info.Name).Replace("_", " "),
                    Extension = info.Extension.TrimStart('.').ToUpperInvariant(),
                    SizeBytes = info.Length,
                    LastModified = info.LastWriteTimeUtc,
                };
            })
            .OrderBy(d => d.DisplayName)
            .ToList();

        return Ok(files);
    }

    /// <summary>
    /// Loads a document from <c>App_Data/documents</c> into an active TX Text Control editor session
    /// identified by the given WebSocket <c>ConnectionID</c>.
    /// </summary>
    /// <param name="request">The request containing the editor connection ID and the file name to load.</param>
    /// <returns>HTTP 200 on success; HTTP 400 or 404 on validation/not-found errors; HTTP 500 on server error.</returns>
    [HttpPost("load")]
    public ActionResult LoadIntoEditor([FromBody] LoadDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionID) || string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest("ConnectionID and FileName are required.");

        var filePath = GetSafeFilePath(request.FileName, _documentsPath);
        if (filePath is null)
            return BadRequest("Invalid file name.");

        if (!System.IO.File.Exists(filePath))
            return NotFound("Document not found.");

        try
        {
            byte[] iufData;
            using (var sc = new TXTextControl.ServerTextControl())
            {
                sc.Create();
                sc.Load(filePath, GetStreamType(filePath));
                sc.Save(out iufData, TXTextControl.BinaryStreamType.InternalUnicodeFormat);
            }

            var wsHandler = WebSocketHandler.GetInstance(request.ConnectionID);
            if (wsHandler is null)
                return BadRequest("No active editor session found for the given ConnectionID.");

            wsHandler.LoadText(iufData, TXTextControl.Web.BinaryStreamType.InternalUnicodeFormat);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load document {FileName} for connection {ConnectionID}", request.FileName, request.ConnectionID);
            return StatusCode(500, "Failed to load document.");
        }
    }

    /// <summary>
    /// Loads a template document from <c>App_Data/documents</c> and returns its content as Base64-encoded
    /// Internal Unicode Format (IUF) for use with the TX Text Control document viewer.
    /// </summary>
    /// <param name="request">The request containing the file name to load.</param>
    /// <returns>A <see cref="ViewerDocumentResponse"/> with the Base64-encoded document data.</returns>
    [HttpPost("viewer")]
    public ActionResult<ViewerDocumentResponse> LoadForViewer([FromBody] ViewerDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest("FileName is required.");

        var filePath = GetSafeFilePath(request.FileName, _documentsPath);
        if (filePath is null)
            return BadRequest("Invalid file name.");

        if (!System.IO.File.Exists(filePath))
            return NotFound("Document not found.");

        try
        {
            byte[] iufData;
            using (var sc = new TXTextControl.ServerTextControl())
            {
                sc.Create();
                sc.Load(filePath, GetStreamType(filePath));
                sc.Save(out iufData, TXTextControl.BinaryStreamType.InternalUnicodeFormat);
            }

            return Ok(new ViewerDocumentResponse { Data = Convert.ToBase64String(iufData) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load document {FileName} for viewer", request.FileName);
            return StatusCode(500, "Failed to load document.");
        }
    }

    /// <summary>
    /// Saves the current content of an active editor session back to a file in <c>App_Data/documents</c>.
    /// </summary>
    /// <param name="request">The request containing the editor connection ID and the target file name.</param>
    /// <returns>HTTP 200 on success; HTTP 400 on validation errors; HTTP 500 on server error.</returns>
    [HttpPost("save")]
    public ActionResult SaveFromEditor([FromBody] SaveDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionID) || string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest("ConnectionID and FileName are required.");

        var filePath = GetSafeFilePath(request.FileName, _documentsPath);
        if (filePath is null)
            return BadRequest("Invalid file name.");

        try
        {
            var wsHandler = WebSocketHandler.GetInstance(request.ConnectionID);
            if (wsHandler is null)
                return BadRequest("No active editor session found for the given ConnectionID.");

            wsHandler.SaveText(filePath, TXTextControl.Web.StreamType.InternalUnicodeFormat);
            _logger.LogInformation("Saved document: {FileName}", request.FileName);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save document {FileName} for connection {ConnectionID}", request.FileName, request.ConnectionID);
            return StatusCode(500, "Failed to save document.");
        }
    }

    /// <summary>
    /// Creates an empty document in an active editor session.
    /// </summary>
    /// <param name="request">The request containing the editor connection ID. The <c>FileName</c> field is ignored.</param>
    /// <returns>HTTP 200 on success; HTTP 400 on validation errors; HTTP 500 on server error.</returns>
    [HttpPost("new")]
    public ActionResult NewDocument([FromBody] LoadDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionID))
            return BadRequest("ConnectionID is required.");

        try
        {
            var wsHandler = WebSocketHandler.GetInstance(request.ConnectionID);
            if (wsHandler is null)
                return BadRequest("No active editor session found for the given ConnectionID.");

            byte[] emptyData;
            using (var sc = new TXTextControl.ServerTextControl())
            {
                sc.Create();
                sc.Save(out emptyData, TXTextControl.BinaryStreamType.InternalUnicodeFormat);
            }

            wsHandler.LoadText(emptyData, TXTextControl.Web.BinaryStreamType.InternalUnicodeFormat);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new document for connection {ConnectionID}", request.ConnectionID);
            return StatusCode(500, "Failed to create new document.");
        }
    }

    /// <summary>
    /// Downloads a document from <c>App_Data/documents</c> as a PDF or DOCX file.
    /// </summary>
    /// <param name="fileName">The name of the document file to download.</param>
    /// <param name="format">The desired output format: <c>pdf</c> (default) or <c>docx</c>.</param>
    /// <returns>The document as a downloadable file; HTTP 400/404/500 on errors.</returns>
    [HttpGet("download")]
    public ActionResult DownloadDocument([FromQuery] string fileName, [FromQuery] string format = "pdf")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest("FileName is required.");

        var filePath = GetSafeFilePath(fileName, _documentsPath);
        if (filePath is null)
            return BadRequest("Invalid file name.");

        if (!System.IO.File.Exists(filePath))
            return NotFound("Document not found.");

        try
        {
            byte[] outputData;
            string contentType;
            string downloadName;
            var baseName = Path.GetFileNameWithoutExtension(fileName);

            using (var sc = new TXTextControl.ServerTextControl())
            {
                sc.Create();
                sc.Load(filePath, GetStreamType(filePath));

                switch (format.ToLowerInvariant())
                {
                    case "docx":
                        sc.Save(out outputData, TXTextControl.BinaryStreamType.WordprocessingML);
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        downloadName = $"{baseName}.docx";
                        break;
                    default:
                        sc.Save(out outputData, TXTextControl.BinaryStreamType.AdobePDF);
                        contentType = "application/pdf";
                        downloadName = $"{baseName}.pdf";
                        break;
                }
            }

            return File(outputData, contentType, downloadName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document {FileName} as {Format}", fileName, format);
            return StatusCode(500, "Failed to download document.");
        }
    }

    // ─── Signature flow ──────────────────────────────────────────────────────

    /// <summary>
    /// Issues a short-lived, one-time signature token that the Angular client must include as the
    /// <c>signatureToken</c> query parameter when the TX Text Control viewer calls the
    /// <c>sign-complete</c> redirect URL. The token expires after the number of minutes configured
    /// in <c>SignatureSettings:TokenExpirationMinutes</c> (default: 15).
    /// </summary>
    /// <returns>A <see cref="SignTokenResponse"/> containing the token and its expiry time.</returns>
    [HttpPost("sign-token")]
    public ActionResult<SignTokenResponse> SignToken()
    {
        var token = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.AddMinutes(_tokenExpirationMinutes);
        var cacheKey = $"{ValidateSignatureTokenAttribute.CacheKeyPrefix}{token}";

        _cache.Set(cacheKey, true, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt
        });

        _logger.LogInformation("Issued signature token expiring at {ExpiresAt}", expiresAt);
        return Ok(new SignTokenResponse { Token = token, ExpiresAt = expiresAt });
    }

    /// <summary>
    /// Processes a completed signature submission from the TX Text Control document viewer.
    /// This endpoint is called by the TX Text Control middleware after the user clicks "Submit"
    /// in the signature bar. It:
    /// <list type="bullet">
    ///   <item>Validates the one-time <c>signatureToken</c> query parameter via <see cref="ValidateSignatureTokenAttribute"/>.</item>
    ///   <item>Applies a cryptographic digital signature using the configured X.509 PFX certificate.</item>
    ///   <item>Saves the signed document as both <c>.tx</c> and <c>.pdf</c> in <c>App_Data/signed-documents</c>.</item>
    ///   <item>Returns the digitally signed PDF as a plain Base64 string for the client-side submit callback.</item>
    /// </list>
    /// </summary>
    /// <param name="data">The signed document data posted by the TX Text Control middleware.</param>
    /// <param name="sourceName">The original template file name (without extension), used as the base name for the stored files.</param>
    /// <returns>A plain Base64 string of the digitally signed PDF; HTTP 500 on server error.</returns>
    [HttpPost("sign-complete")]
    [ValidateSignatureToken]
    public ActionResult<string> SignComplete(
        [FromBody] TXTextControl.Web.MVC.DocumentViewer.Models.SignatureData data,
        [FromQuery] string? sourceName = null)
    {
        try
        {
            var baseName = SanitizeFileName(sourceName ?? "signed_document");
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileBaseName = $"{baseName}_{timestamp}";

            byte[] iufData;
            byte[] pdfData;

            using (var sc = new TXTextControl.ServerTextControl())
            {
                sc.Create();
                sc.Load(Convert.FromBase64String(data.SignedDocument.Document),
                        TXTextControl.BinaryStreamType.InternalUnicodeFormat);

                // Save the electronically signed document as .tx (preserves signature fields and marks)
                sc.Save(out iufData, TXTextControl.BinaryStreamType.InternalUnicodeFormat);

                // Apply digital X.509 signature and save as PDF
                if (System.IO.File.Exists(_pfxPath))
                {
                    var cert = X509CertificateLoader.LoadPkcs12FromFile(_pfxPath, _pfxPassword);
                    var saveSettings = new TXTextControl.SaveSettings
                    {
                        SignatureFields = new TXTextControl.DigitalSignature[]
                        {
                            new TXTextControl.DigitalSignature(cert, null, "txsign")
                        }
                    };
                    sc.Save(out pdfData, TXTextControl.BinaryStreamType.AdobePDF, saveSettings);
                    _logger.LogInformation("Applied digital signature from PFX certificate.");
                }
                else
                {
                    _logger.LogWarning("PFX file not found at {PfxPath}; saving PDF without digital signature.", _pfxPath);
                    sc.Save(out pdfData, TXTextControl.BinaryStreamType.AdobePDF);
                }
            }

            // Persist signed copies
            var txPath = Path.Combine(_signedDocumentsPath, $"{fileBaseName}.tx");
            var pdfPath = Path.Combine(_signedDocumentsPath, $"{fileBaseName}.pdf");
            System.IO.File.WriteAllBytes(txPath, iufData);
            System.IO.File.WriteAllBytes(pdfPath, pdfData);
            _logger.LogInformation("Stored signed document: {BaseName} (.tx + .pdf)", fileBaseName);

            return Content(Convert.ToBase64String(pdfData), "text/plain");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process signed document");
            return StatusCode(500, "Failed to process signed document.");
        }
    }

    // ─── Signed documents ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the list of previously signed documents stored in <c>App_Data/signed-documents</c>.
    /// Only <c>.tx</c> files are listed (each has a corresponding <c>.pdf</c>).
    /// The list is ordered by signing time, newest first.
    /// </summary>
    /// <returns>An ordered list of <see cref="SignedDocumentInfo"/> objects.</returns>
    [HttpGet("signed")]
    public ActionResult<IEnumerable<SignedDocumentInfo>> ListSigned()
    {
        if (!Directory.Exists(_signedDocumentsPath))
            return Ok(Array.Empty<SignedDocumentInfo>());

        var items = Directory.GetFiles(_signedDocumentsPath, "*.tx")
            .Select(f =>
            {
                var info = new FileInfo(f);
                var (displayName, signedAt) = ParseSignedFileName(info.Name);
                return new SignedDocumentInfo
                {
                    FileName = info.Name,
                    DisplayName = displayName,
                    Extension = "TX",
                    SignedAt = signedAt,
                    SizeBytes = info.Length,
                };
            })
            .OrderByDescending(d => d.SignedAt)
            .ToList();

        return Ok(items);
    }

    /// <summary>
    /// Downloads the PDF version of a previously signed document from <c>App_Data/signed-documents</c>.
    /// </summary>
    /// <param name="fileName">The <c>.tx</c> or <c>.pdf</c> file name of the signed document.</param>
    /// <returns>The PDF file as a download; HTTP 400/404/500 on errors.</returns>
    [HttpGet("signed/download")]
    public ActionResult DownloadSigned([FromQuery] string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest("FileName is required.");

        // Always serve the PDF version
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var pdfName = $"{baseName}.pdf";
        var filePath = GetSafeFilePath(pdfName, _signedDocumentsPath);
        if (filePath is null)
            return BadRequest("Invalid file name.");

        if (!System.IO.File.Exists(filePath))
            return NotFound("Signed document not found.");

        var bytes = System.IO.File.ReadAllBytes(filePath);
        return File(bytes, "application/pdf", pdfName);
    }

    /// <summary>
    /// Loads a previously signed document from <c>App_Data/signed-documents</c> and returns its
    /// content as Base64-encoded IUF for use with the TX Text Control document viewer.
    /// </summary>
    /// <param name="request">The request containing the file name of the signed document to load.</param>
    /// <returns>A <see cref="ViewerDocumentResponse"/> with the Base64-encoded document data.</returns>
    [HttpPost("signed/viewer")]
    public ActionResult<ViewerDocumentResponse> LoadSignedForViewer([FromBody] ViewerSignedDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest("FileName is required.");

        // Prefer the .tx version for the viewer (preserves signature annotations)
        var baseName = Path.GetFileNameWithoutExtension(request.FileName);
        var txName = $"{baseName}.tx";
        var filePath = GetSafeFilePath(txName, _signedDocumentsPath);
        if (filePath is null)
            return BadRequest("Invalid file name.");

        if (!System.IO.File.Exists(filePath))
            return NotFound("Signed document not found.");

        try
        {
            byte[] iufData;
            using (var sc = new TXTextControl.ServerTextControl())
            {
                sc.Create();
                sc.Load(filePath, TXTextControl.StreamType.InternalUnicodeFormat);
                sc.Save(out iufData, TXTextControl.BinaryStreamType.InternalUnicodeFormat);
            }

            return Ok(new ViewerDocumentResponse { Data = Convert.ToBase64String(iufData) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load signed document {FileName} for viewer", request.FileName);
            return StatusCode(500, "Failed to load signed document.");
        }
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Maps a file extension to the appropriate TX Text Control <see cref="TXTextControl.StreamType"/>.
    /// </summary>
    /// <param name="filePath">The full path of the file whose stream type should be determined.</param>
    /// <returns>The matching <see cref="TXTextControl.StreamType"/>.</returns>
    private static TXTextControl.StreamType GetStreamType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".tx" => TXTextControl.StreamType.InternalUnicodeFormat,
            ".docx" => TXTextControl.StreamType.WordprocessingML,
            ".rtf" => TXTextControl.StreamType.RichTextFormat,
            ".txt" => TXTextControl.StreamType.PlainText,
            ".pdf" => TXTextControl.StreamType.AdobePDF,
            _ => TXTextControl.StreamType.InternalUnicodeFormat,
        };
    }

    /// <summary>
    /// Returns the full, canonicalized path for <paramref name="fileName"/> within <paramref name="basePath"/>,
    /// or <c>null</c> if the resolved path escapes the base directory (path traversal protection).
    /// </summary>
    /// <param name="fileName">The untrusted file name received from the client.</param>
    /// <param name="basePath">The base directory that the resolved path must remain within.</param>
    /// <returns>The safe full path, or <c>null</c> if the name is invalid or traverses outside the base.</returns>
    private static string? GetSafeFilePath(string fileName, string basePath)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || safeName != fileName)
            return null;

        var fullPath = Path.GetFullPath(Path.Combine(basePath, safeName));
        if (!fullPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            return null;

        return fullPath;
    }

    /// <summary>
    /// Removes characters from <paramref name="name"/> that are unsafe for use in a file name.
    /// </summary>
    /// <param name="name">The raw name to sanitize.</param>
    /// <returns>A sanitized version of <paramref name="name"/>, falling back to <c>"document"</c> if empty.</returns>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray())
            .Trim()
            .Replace(' ', '_');
        return string.IsNullOrEmpty(sanitized) ? "document" : sanitized;
    }

    /// <summary>
    /// Parses a signed document file name (format: <c>{baseName}_{yyyyMMdd_HHmmss}.tx</c>) into
    /// a human-readable display name and the signing timestamp.
    /// </summary>
    /// <param name="fileName">The file name including extension.</param>
    /// <returns>A tuple of the display name and the parsed <see cref="DateTime"/> (UTC).</returns>
    private static (string DisplayName, DateTime SignedAt) ParseSignedFileName(string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // Expected pattern: {baseName}_{yyyyMMdd_HHmmss}
        // Split from the right: the last two underscore-separated segments are the timestamp
        var parts = nameWithoutExt.Split('_');
        if (parts.Length >= 3 &&
            DateTime.TryParseExact(
                $"{parts[^2]}_{parts[^1]}",
                "yyyyMMdd_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var ts))
        {
            var displayName = string.Join(" ", parts[..^2]).Replace("_", " ");
            return (displayName, DateTime.SpecifyKind(ts, DateTimeKind.Utc));
        }

        return (nameWithoutExt.Replace("_", " "), DateTime.UtcNow);
    }

    /// <summary>
    /// Ensures the three built-in sample documents exist in <c>App_Data/documents</c>.
    /// Called once at controller construction time.
    /// </summary>
    private void EnsureSampleDocuments()
    {
        CreateSampleIfMissing("Welcome_Letter.tx", CreateWelcomeLetter);
        CreateSampleIfMissing("Contract_Template.tx", CreateContractTemplate);
        CreateSampleIfMissing("NDA_Template.tx", CreateNdaTemplate);
    }

    /// <summary>
    /// Creates a sample document using the given builder action if the file does not yet exist.
    /// </summary>
    /// <param name="fileName">The target file name within <c>App_Data/documents</c>.</param>
    /// <param name="builder">An action that populates a <see cref="TXTextControl.ServerTextControl"/> instance with content.</param>
    private void CreateSampleIfMissing(string fileName, Action<TXTextControl.ServerTextControl> builder)
    {
        var filePath = Path.Combine(_documentsPath, fileName);
        if (System.IO.File.Exists(filePath))
            return;

        try
        {
            using var sc = new TXTextControl.ServerTextControl();
            sc.Create();
            builder(sc);
            sc.Save(filePath, TXTextControl.StreamType.InternalUnicodeFormat);
            _logger.LogInformation("Created sample document: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create sample document {FileName}. TX Text Control runtime may not be available.", fileName);
        }
    }

    /// <summary>Populates a <see cref="TXTextControl.ServerTextControl"/> with welcome letter content.</summary>
    private static void CreateWelcomeLetter(TXTextControl.ServerTextControl sc)
    {
        sc.Selection.FontSize = 24 * 20;
        sc.Selection.Bold = true;
        sc.Selection.Text = "Welcome to TX Text Control\r\n";

        sc.Selection.FontSize = 12 * 20;
        sc.Selection.Bold = false;
        sc.Selection.Text = "\r\nDear User,\r\n\r\n";

        sc.Selection.Text =
            "Thank you for exploring the TX Text Control document editor demo. " +
            "This application demonstrates the full capabilities of the TX Text Control " +
            "document processing SDK integrated with Angular and .NET Aspire.\r\n\r\n";

        sc.Selection.Text =
            "With TX Text Control, you can:\r\n" +
            "  - Create and edit Word-compatible documents\r\n" +
            "  - Add form fields and signature fields\r\n" +
            "  - Generate documents from templates\r\n" +
            "  - View and sign documents electronically\r\n\r\n";

        sc.Selection.Text = "Best regards,\r\nThe TX Text Control Team\r\n";
    }

    /// <summary>Populates a <see cref="TXTextControl.ServerTextControl"/> with a service agreement template including a signature field.</summary>
    private static void CreateContractTemplate(TXTextControl.ServerTextControl sc)
    {
        sc.Selection.FontSize = 24 * 20;
        sc.Selection.Bold = true;
        sc.Selection.Text = "Service Agreement\r\n";

        sc.Selection.FontSize = 12 * 20;
        sc.Selection.Bold = false;
        sc.Selection.Text = "\r\nThis Service Agreement (\"Agreement\") is entered into as of the date of last signature below.\r\n\r\n";

        sc.Selection.Bold = true;
        sc.Selection.Text = "1. Services\r\n";
        sc.Selection.Bold = false;
        sc.Selection.Text = "The Provider agrees to deliver document processing services as described in Exhibit A attached hereto.\r\n\r\n";

        sc.Selection.Bold = true;
        sc.Selection.Text = "2. Term\r\n";
        sc.Selection.Bold = false;
        sc.Selection.Text = "This Agreement shall commence on the Effective Date and continue for a period of twelve (12) months, unless terminated earlier in accordance with Section 5.\r\n\r\n";

        sc.Selection.Bold = true;
        sc.Selection.Text = "3. Compensation\r\n";
        sc.Selection.Bold = false;
        sc.Selection.Text = "Client shall pay Provider the fees set forth in Exhibit B within thirty (30) days of receipt of each invoice.\r\n\r\n";

        sc.Selection.Bold = true;
        sc.Selection.Text = "4. Confidentiality\r\n";
        sc.Selection.Bold = false;
        sc.Selection.Text = "Each party agrees to maintain the confidentiality of all proprietary information disclosed by the other party during the term of this Agreement.\r\n\r\n";

        sc.Selection.Bold = true;
        sc.Selection.Text = "5. Termination\r\n";
        sc.Selection.Bold = false;
        sc.Selection.Text = "Either party may terminate this Agreement with thirty (30) days written notice to the other party.\r\n\r\n";

        sc.Selection.Text = "\r\n\r\n____________________________\r\nAuthorized Signature\r\n";

        try
        {
            var sig = new TXTextControl.SignatureField(new System.Drawing.Size(4000, 1600), "txsign", 0);
            sc.SignatureFields.Add(sig, -1);
        }
        catch { /* Signature fields may require specific licensing */ }
    }

    /// <summary>Populates a <see cref="TXTextControl.ServerTextControl"/> with an NDA template including two signature fields.</summary>
    private static void CreateNdaTemplate(TXTextControl.ServerTextControl sc)
    {
        sc.Selection.FontSize = 24 * 20;
        sc.Selection.Bold = true;
        sc.Selection.Text = "Non-Disclosure Agreement\r\n";

        sc.Selection.FontSize = 12 * 20;
        sc.Selection.Bold = false;
        sc.Selection.Text = "\r\nThis Non-Disclosure Agreement (\"NDA\") is made effective as of the date signed below.\r\n\r\n";

        sc.Selection.Bold = true;
        sc.Selection.Text = "1. Definition of Confidential Information\r\n";
        sc.Selection.Bold = false;
        sc.Selection.Text = "\"Confidential Information\" means any data or information that is proprietary to the Disclosing Party, including but not limited to trade secrets, technology, research, business plans, and financial information.\r\n\r\n";

        sc.Selection.Bold = true;
        sc.Selection.Text = "2. Obligations of Receiving Party\r\n";
        sc.Selection.Bold = false;
        sc.Selection.Text = "The Receiving Party agrees to:\r\n" +
            "  a) Hold and maintain Confidential Information in strict confidence\r\n" +
            "  b) Not disclose Confidential Information to any third parties\r\n" +
            "  c) Not use Confidential Information for any purpose other than as authorized\r\n\r\n";

        sc.Selection.Bold = true;
        sc.Selection.Text = "3. Duration\r\n";
        sc.Selection.Bold = false;
        sc.Selection.Text = "The obligations of this NDA shall remain in effect for a period of two (2) years from the date of disclosure of Confidential Information.\r\n\r\n";

        sc.Selection.Bold = true;
        sc.Selection.Text = "4. Return of Materials\r\n";
        sc.Selection.Bold = false;
        sc.Selection.Text = "Upon termination or expiration of this NDA, the Receiving Party shall promptly return or destroy all materials containing Confidential Information.\r\n\r\n";

        sc.Selection.Text = "\r\n\r\n____________________________\r\nSignature\r\n";
        sc.Selection.Text = "\r\n________\r\nInitials\r\n";

        try
        {
            var sig = new TXTextControl.SignatureField(new System.Drawing.Size(4000, 1600), "txsign", 0);
            sc.SignatureFields.Add(sig, -1);

            var initials = new TXTextControl.SignatureField(new System.Drawing.Size(2000, 800), "txinitials", 1);
            sc.SignatureFields.Add(initials, -1);
        }
        catch { /* Signature fields may require specific licensing */ }
    }
}
