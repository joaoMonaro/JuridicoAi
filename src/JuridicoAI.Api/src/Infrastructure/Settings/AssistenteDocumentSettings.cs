namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class AssistenteDocumentSettings
{
    public bool Enabled { get; set; }
    // OCR must be explicitly calibrated for the deployment/model set.
    public bool OcrEnabled { get; set; }
    public string OcrCalibrationVersion { get; set; } = "";
    public string PdfInfoPath { get; set; } = "pdfinfo";
    public string PdfTextPath { get; set; } = "pdftotext";
    public string PdfImagesPath { get; set; } = "pdfimages";
    public string PdfRenderPath { get; set; } = "pdftoppm";
    public string TesseractPath { get; set; } = "tesseract";
    public string TessdataPath { get; set; } = "";
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;
    public long MaxConversationBytes { get; set; } = 500L * 1024 * 1024;
    public int MaxAttachments { get; set; } = 50;
    public int MaxPages { get; set; } = 100;
    public int MaxPixels { get; set; } = 25_000_000;
    public int MaxExtractedCharacters { get; set; } = 2_000_000;
    public int ProcessTimeoutSeconds { get; set; } = 60;
    public int LeaseSeconds { get; set; } = 240;
    public int MaxAttempts { get; set; } = 3;
}
