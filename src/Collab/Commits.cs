using StepWise.Prose.Model;
using StepWise.Prose.Transformation;


namespace StepWise.Prose.Collab;

/// <summary>
/// Contains the data necessary to construct a document of supplied version.
/// </summary>
public record DocMaterial {
    public required int
    Version { get; init; }
    public required Node
    BaseDoc { get; init; }
    public required List<ICommit>
    Commits { get; init; }
}

public interface ICommit
{
    int Version { get; init; }
    string Ref { get; init; }
    List<Step> Steps { get; init; }
}

/// <summary>
/// Represents an atomic document change comprised of one or more steps. The ref must
/// unique across all commits on a document.
/// </summary>
/// <param name="Version">The authority confirmed document version the commit steps were created from.</param>
/// <param name="Ref">A unique string across all commits on a single document.</param>
/// <param name="Steps">A list of transform steps to commit.</param>
public record Commit(
    int Version,
    string Ref,
    List<Step> Steps
) : ICommit;