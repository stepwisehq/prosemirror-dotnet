using StepWise.Prose.Collections;
using StepWise.Prose.Model;
using StepWise.Prose.Transformation;


namespace StepWise.Prose.Collab;

public static class Collab {

    /// <summary>
    /// Applies a commit to a document, mapping the commit through the document's history starting from the Commit's
    /// version.
    /// </summary>
    /// <param name="material">The current document material.</param>
    /// <param name="commits">The list of commits in the document's history since the commit version.</param>
    /// <param name="commit">The commit to apply.</param>
    /// <returns>A tuple containing the updated document and the mapped commit.</returns>
    public static (DocMaterial, Commit) ApplyCommit(DocMaterial material, List<Commit> commits, Commit commit) {
        var steps = material.Commits.Aggregate((List<Step>) new(), (steps, c) => steps.Concat(c.Steps).ToList());
        var tr = new Transform(material.BaseDoc);
        steps.ForEach(s => tr.Step(s));
        var (doc, mappedCommit) = ApplyCommit(material.Version, tr.Doc, commits, commit);

        return (
            new() { Version = mappedCommit.Version, BaseDoc = doc, Commits = new() },
            mappedCommit
        );
    }

    /// <summary>
    /// Applies a commit to a document, mapping the commit through the document's history starting from the Commit's
    /// version.
    /// </summary>
    /// <param name="version">The current version of the document.</param>
    /// <param name="doc">The document to apply the commit to.</param>
    /// <param name="commits">The list of commits in the document's history since the commit version.</param>
    /// <param name="commit">The commit to apply.</param>
    /// <returns>A tuple containing the updated document and the mapped commit.</returns>
    public static (Node, Commit) ApplyCommit(int version, Node doc, List<Commit> commits, Commit commit) {
        var newSteps = commits.Aggregate((List<Step>) new(), (steps, c) => steps.Concat(c.Steps).ToList());
        var newStepMap = new Mapping(newSteps.Select(s => s.GetMap()).ToList());

        // We want to map the commit steps through their predecessor's inverse in case one of them gets
        // dropped through the process of mapping and applying.
        var commitSteps = commit.Steps;
        var mapping = new Mapping(commitSteps.Select(s => s.GetMap()).ToList()).Invert();
        mapping.AppendMapping(newStepMap); // Mapping is now [...InvertedCommitMaps, ...NewStepMaps]

        var tr = new Transform(doc);

        for (int i = 0, mapFrom = commitSteps.Count; i < commitSteps.Count; i++) {
            var step = commitSteps[i];
            var sliced = mapping.Slice(mapFrom);
            var mapped = step!.Map(sliced)!;
            mapFrom--;
            if (mapped is not null && tr.MaybeStep(mapped).Failed is null) {
                mapping.AppendMapping(new (tr.Mapping.Maps.slice(tr.Steps.Count - 1)));
                // Set mirror so positions can be recovered properly. Without this a Replace.To
                // that landed in a position created by a predecessor would not get mapped back to the correct
                // position.
                mapping.SetMirror(mapFrom, mapping.Maps.Count - 1);
            }
        }

        version++;
        Commit mappedCommit = new(version, commit.Ref, tr.Steps);
        return (tr.Doc, mappedCommit);
    }
}