namespace CompanyCopilot.Application.Configuration;

public class OllamaOptions
{
    public const string Section = "Ollama";

    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";
    public string ChatModel { get; set; } = "empresa-copiloto:v1";
    public string EmbeddingModel { get; set; } = "embeddinggemma";
    public int ContextLength { get; set; } = 8192;
    public int EmbeddingDimensions { get; set; } = 768;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(180);
}

public class ChatOptions
{
    public const string Section = "Chat";

    public int MaxQuestionCharacters { get; set; } = 2000;
    public int MaxHistoryTurns { get; set; } = 6;
    public int SessionIdleMinutes { get; set; } = 20;
    public int MaxOutputTokens { get; set; } = 512;
    public int MaxRetrievedChunks { get; set; } = 6;
    public int MaxQuestionsPerWindow { get; set; } = 8;
    public int RateLimitWindowMinutes { get; set; } = 10;
    public int MaxConcurrentGenerations { get; set; } = 1;
    public int MaxQueuedGenerations { get; set; } = 5;
}

public class StorageOptions
{
    public const string Section = "Storage";

    public string Root { get; set; } = "./data";
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } =
        { ".pdf", ".docx", ".xlsx", ".txt", ".md", ".html", ".htm" };
}

public class VectorSearchOptions
{
    public const string Section = "VectorSearch";

    public int VectorSearchTake { get; set; } = 10;
    public int TextSearchTake { get; set; } = 10;
    public double RrfK { get; set; } = 60;
    public double ConflictSimilarityMin { get; set; } = 0.55;
    public double ConflictSimilarityMax { get; set; } = 0.97;
}
