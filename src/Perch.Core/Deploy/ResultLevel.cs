namespace Perch.Core.Deploy;

/// <summary>Represents the result level of a deploy operation.</summary>
public enum ResultLevel
{
    /// <summary>Already in sync, no action needed.</summary>
    Synced,

    /// <summary>Action completed successfully.</summary>
    Ok,

    /// <summary>Action completed with warnings.</summary>
    Warning,

    /// <summary>Action failed.</summary>
    Error,
}
