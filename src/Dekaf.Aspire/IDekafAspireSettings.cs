namespace Dekaf.Aspire;

/// <summary>Settings shared by every Dekaf Aspire registration.</summary>
internal interface IDekafAspireSettings
{
    string? ConnectionString { get; set; }
    bool DisableHealthChecks { get; }
    bool DisableMetrics { get; }
    bool DisableTracing { get; }
}
