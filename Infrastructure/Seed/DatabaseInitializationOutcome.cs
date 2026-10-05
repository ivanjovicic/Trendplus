namespace Infrastructure.Seed;

public sealed record DatabaseInitializationOutcome(
    bool CompletedWithErrors,
    string? FailureCategory = null,
    string? FailureStage = null)
{
    public static DatabaseInitializationOutcome Succeeded { get; } = new(false);

    public static DatabaseInitializationOutcome WithErrors(string failureCategory, string failureStage) =>
        new(true, failureCategory, failureStage);
}

public sealed class DatabaseInitializationFailureException : InvalidOperationException
{
    public DatabaseInitializationFailureException(
        string failureCategory,
        string failureStage,
        Exception innerException)
        : base("Database initialization failed.", innerException)
    {
        FailureCategory = failureCategory;
        FailureStage = failureStage;
    }

    public string FailureCategory { get; }

    public string FailureStage { get; }
}
