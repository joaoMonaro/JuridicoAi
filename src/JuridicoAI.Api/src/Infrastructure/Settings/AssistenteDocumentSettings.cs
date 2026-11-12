namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class AssistenteDocumentSettings
{
    public bool Enabled { get; set; }
    public bool RetrievalEnabled { get; set; }
    public int MaxRetrievalRounds { get; set; } = 8;
    public int MaxRetrievalTotalTokens { get; set; } = 180_000;
    public int MaxRetrievalResultCharacters { get; set; } = 12_000;
    public bool AnalysisEnabled { get; set; }
    public int MaxAnalysisCalls { get; set; } = 12;
    public int MaxAnalysisTotalTokens { get; set; } = 180_000;
    public int AnalysisBatchCharacters { get; set; } = 24_000;
    public int AnalysisSummaryCharacters { get; set; } = 8_000;
    public int AnalysisOutputTokens { get; set; } = 2_000;
    public int AnalysisLeaseSeconds { get; set; } = 180;
    public int AnalysisDeadlineSeconds { get; set; } = 600;
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
