namespace CompanyCopilot.Domain.Enums;

public enum DocumentStatus
{
    Draft = 0,
    Processing = 1,
    Approved = 2,
    Archived = 3,
    Rejected = 4,
    Failed = 5
}

public enum Visibility
{
    Public = 0,
    Internal = 1
}

public enum DocumentPriority
{
    Informational = 0,
    Standard = 1,
    Authoritative = 2
}

public enum DocumentCategory
{
    Other = 0,
    Institutional = 1,
    Faq = 2,
    Rules = 3,
    PaymentConditions = 4,
    Hours = 5,
    Policy = 6
}

public enum IngestionJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}

public enum AuditEventType
{
    DocumentImported = 0,
    DocumentFailed = 1,
    MetadataUpdated = 2,
    DocumentApproved = 3,
    DocumentArchived = 4,
    DocumentRejected = 5,
    DocumentReindexed = 6,
    DocumentUnpublished = 7,
    TestQuestionExecuted = 8,
    FeedbackReceived = 9,
    DocumentDeleted = 10
}

public enum FeedbackKind
{
    Positive = 0,
    Negative = 1
}

public enum EvaluationResult
{
    Pending = 0,
    Passed = 1,
    Failed = 2
}
