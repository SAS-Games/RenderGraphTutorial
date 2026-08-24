namespace SAS.RenderDebugging
{
    /// <summary>
    /// Identifies a rendering system that can publish ordered debug stages.
    /// Implementations may be renderer features, render passes, behaviours, or plain C# objects.
    /// </summary>
    public interface IRenderDebugSource
    {
        /// <summary>Gets the stable, project-unique source identifier.</summary>
        string DebugId { get; }

        /// <summary>Gets the human-readable source name shown by the viewer.</summary>
        string DisplayName { get; }
    }
}
