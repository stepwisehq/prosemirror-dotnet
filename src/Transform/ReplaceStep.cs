using System.Text.Json.Serialization;
using StepWise.Prose.Model;


namespace StepWise.Prose.Transformation;

public class ReplaceStep : Step {
    public int From { get; }
    public int To { get; }
    public Slice Slice { get; }
    public bool Structure { get; }

    public ReplaceStep(int from, int to, Slice slice, bool structure = false) {
        From = from;
        To = to;
        Slice = slice;
        Structure = structure;
    }

    public override StepResult Apply(Node doc) {
        if (Structure && ReplaceStepUtil.ContentBetween(doc, From, To))
            return StepResult.Fail("Structure replace would overwrite content");
        return StepResult.FromReplace(doc, From, To, Slice);
    }


    public override StepMap GetMap() =>
        new(new() {From, To - From, Slice.Size});

    public override ReplaceStep Invert(Node doc) =>
        new(From, From + Slice.Size, doc.Slice(From, To));

    public override ReplaceStep? Map(IMappable mapping) {
        var from = mapping.MapResult(From, 1);
        var to = mapping.MapResult(To, -1);
        if (from.DeletedAcross && to.DeletedAcross) return null;
        return new(from.Pos, Math.Max(from.Pos, to.Pos), Slice);
    }

    public override ReplaceStep? Merge(Step other) {
        if (other is not ReplaceStep rs || rs.Structure || Structure) return null;

        if (From + Slice.Size == rs.From && Slice.OpenEnd == 0 && Slice.OpenStart == 0) {
            var slice = Slice.Size + rs.Slice.Size == 0 ? Slice.Empty
                : new Slice(Slice.Content.Append(rs.Slice.Content), Slice.OpenStart, rs.Slice.OpenEnd);
            return new(From, To + (rs.To - rs.From), slice, Structure);
        } else if (rs.To == From && Slice.OpenStart == 0 && Slice.OpenEnd == 0) {
            var slice = Slice.Size + rs.Slice.Size == 0 ? Slice.Empty
                : new Slice(rs.Slice.Content.Append(Slice.Content), rs.Slice.OpenStart, Slice.OpenEnd);
            return new(rs.From, To, slice, Structure);
        } else {
            return null;
        }
    }

    public override ReplaceStepDto ToJSON() {
        return new() {
            From = From,
            To = To,
            Slice = Slice.Size == 0 ? DotNext.Optional<SliceDto>.None : Slice.ToJSON(),
            Structure = Structure ? true : DotNext.Optional<bool?>.None
        };
    }

    public static ReplaceStep FromJSON(Schema schema, ReplaceStepDto json) {
        var sliceJson = json.Slice.HasValue ? json.Slice.Value : null;
        var structure = json.Structure.HasValue && (bool)json.Structure.Value!;
        return new(json.From, json.To, Slice.FromJSON(schema, sliceJson), structure);
    }
}

public class ReplaceAroundStep : Step {
    public int From { get; }
    public int To { get; }
    public int GapFrom { get; }
    public int GapTo { get; }
    public Slice Slice { get; }
    public int Insert { get; }
    public bool Structure { get; }

    public ReplaceAroundStep(
        int from,
        int to,
        int gapFrom,
        int gapTo,
        Slice slice,
        int insert,
        bool structure = false)
    {
        From = from;
        To = to;
        GapFrom = gapFrom;
        GapTo = gapTo;
        Slice = slice;
        Insert = insert;
        Structure = structure;
    }

    public override StepResult Apply(Node doc) {
        if (Structure && (ReplaceStepUtil.ContentBetween(doc, From, GapFrom) ||
                          ReplaceStepUtil.ContentBetween(doc, GapTo, To)))
            return StepResult.Fail("Structure gap-replace would overwrite content");

        var gap = doc.Slice(GapFrom, GapTo);
        if (gap.OpenStart > 0 || gap.OpenEnd > 0)
            return StepResult.Fail("Gap is not a flat range");
        var inserted = Slice.InsertAt(Insert, gap.Content);
        if (inserted is null) return StepResult.Fail("Content does not fit in gap");
        return StepResult.FromReplace(doc, From, To, inserted);
    }

    public override StepMap GetMap() =>
        new(new(){From, GapFrom - From, Insert,
                  GapTo, To - GapTo, Slice.Size - Insert});

    public override ReplaceAroundStep Invert(Node doc) {
        var gap = GapTo - GapFrom;
        return new(From, From + Slice.Size + gap,
                   From + Insert, From + Insert + gap,
                   doc.Slice(From, To).RemoveBetween(GapFrom - From, GapTo - From),
                   GapFrom - From, Structure);
    }

    public override ReplaceAroundStep? Map(IMappable mapping) {
        var from = mapping.MapResult(From, 1);
        var to = mapping.MapResult(To, -1);
        var gapFrom = mapping.Map(GapFrom, -1);
        var gapTo = mapping.Map(GapTo, 1);
        if ((from.DeletedAcross && to.DeletedAcross) || gapFrom < from.Pos || gapTo > to.Pos) return null;
        return new(from.Pos, to.Pos, gapFrom, gapTo, Slice, Insert, Structure);
    }

    public override ReplaceAroundStepDto ToJSON() {
        return new() {
            From = From,
            To = To,
            GapFrom = GapFrom,
            GapTo = GapTo,
            Insert = Insert,
            Slice = Slice.Size == 0 ? DotNext.Optional<SliceDto>.None : Slice.ToJSON(),
            Structure = Structure ? true : DotNext.Optional<bool?>.None
        };
    }

    public static ReplaceAroundStep FromJSON(Schema schema, ReplaceAroundStepDto json) {
        var sliceJson = json.Slice.HasValue ? json.Slice.Value : null;
        var structure = json.Structure.HasValue && (bool)json.Structure.Value!;
        return new(json.From, json.To, json.GapFrom, json.GapTo,
                   Slice.FromJSON(schema, sliceJson), json.Insert, structure);
    }

}

public static class ReplaceStepUtil {
    public static bool ContentBetween(Node doc, int from, int to) {
        var _from = doc.Resolve(from);
        int dist = to - from, depth = _from.Depth;
        while (dist > 0 && depth > 0 && _from.IndexAfter(depth) == _from.Node(depth).ChildCount) {
            depth--;
            dist--;
        }
        if (dist > 0) {
            var next = _from.Node(depth).MaybeChild(_from.IndexAfter(depth));
            while (dist > 0) {
                if (next is null || next.IsLeaf) return true;
                next = next.FirstChild;
                dist--;
            }
        }
        return false;
    }
}

public record ReplaceStepDto : StepDto {
    [JsonIgnore]
    public override string StepType { get; } = "replace";
    public required int From { get; init; }
    public required int To { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DotNext.Optional<SliceDto> Slice { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DotNext.Optional<bool?> Structure { get; init; } = false;
}

public record ReplaceAroundStepDto : StepDto {
    [JsonIgnore]
    public override string StepType { get; } = "replaceAround";
    public required int From { get; init; }
    public required int To { get; init; }
    public required int GapFrom { get; init; }
    public required int GapTo { get; init; }
    public required int Insert { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DotNext.Optional<SliceDto> Slice { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DotNext.Optional<bool?> Structure { get; init; } = false;
}