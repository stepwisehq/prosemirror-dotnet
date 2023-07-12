using System.Text.Json;
using OneOf;
using StepWise.Prose.Collections;


namespace StepWise.Prose.Transformation;

public interface IMappable {
    public int Map(int pos, int assoc);
    public MapResult MapResult(int pos, int assoc);
}

file static class MapUtil {
    public const long lower16 = 0xffff;
    public static long factor16 = (long)Math.Pow(2, 16);

    public static double MakeRecover(int index, int offset) => index + offset * factor16;
    public static int RecoverIndex(double value) => (int)((long)value & lower16);
    public static int RecoverOffset(double value) => (int)((value - ((long)value & lower16)) / factor16);
}

public class MapResult {
    public const int DEL_BEFORE = 1;
    public const int DEL_AFTER = 2;
    public const int DEL_ACROSS = 4;
    public const int DEL_SIDE = 8;

    public int Pos { get; init; }
    public int DelInfo { get; init; }
    public double? Recover { get; init; }

    public MapResult(int pos, int delInfo, double? recover) {
        Pos = pos;
        DelInfo = delInfo;
        Recover = recover;
    }

    public bool Deleted => (DelInfo & DEL_SIDE) > 0;
    public bool DeletedBefore => (DelInfo & (DEL_BEFORE | DEL_ACROSS)) > 0;
    public bool DeletedAfter => (DelInfo & (DEL_AFTER | DEL_ACROSS)) > 0;
    public bool DeletedAcross => (DelInfo & DEL_ACROSS) > 0;
}

public class StepMap : IMappable {
    public List<int> Ranges { get; }
    public readonly bool Inverted;

    public StepMap(List<int> ranges, bool inverted = false) {
        Ranges = ranges;
        Inverted = inverted;
    }

    public int Recover(double value) {
        int diff = 0, index = MapUtil.RecoverIndex(value);
        if (!Inverted) for (var i = 0; i < index; i++)
            diff += Ranges[i * 3 + 2] - Ranges[i * 3 + 1];
        return Ranges[index * 3] + diff + MapUtil.RecoverOffset(value);
    }

    public MapResult MapResult(int pos, int assoc = 1) =>
        (MapResult)_map(pos, assoc, false).Value;

    public int Map(int pos, int assoc = 1) =>
        (int)_map(pos, assoc, true).Value;

    public OneOf<int, MapResult> _map(int pos, int assoc, bool simple) {
        int diff = 0, oldIndex = Inverted ? 2 : 1, newIndex = Inverted ? 1 : 2;
        for (var i = 0; i < Ranges.Count; i += 3) {
            var start = Ranges[i] - (Inverted ? diff : 0);
            if (start > pos) break;
            int oldSize = Ranges[i + oldIndex], newSize = Ranges[i + newIndex], end = start + oldSize;
            if (pos <= end) {
                int side = oldSize == 0 ? assoc : pos == start ? -1 : pos == end ? 1 : assoc;
                var result = start + diff + (side < 0 ? 0 : newSize);
                if (simple) return result;
                double? recover = pos == (assoc < 0 ? start : end) ? null : MapUtil.MakeRecover(i / 3, pos - start);
                var del = pos == start ? Transformation.MapResult.DEL_AFTER : pos == end ? Transformation.MapResult.DEL_BEFORE : Transformation.MapResult.DEL_ACROSS;
                if (assoc < 0 ? pos != start : pos != end) del |= Transformation.MapResult.DEL_SIDE;
                return new MapResult(result, del, recover);
            }
            diff += newSize - oldSize;
        }
        return simple ? pos + diff : new MapResult(pos + diff, 0, null);
    }

    public bool Touches(int pos, double recover) {
        int diff = 0, index = MapUtil.RecoverIndex(recover);
        int oldIndex = Inverted ? 2 : 1, newIndex = Inverted ? 1 : 2;
        for (var i = 0; i < Ranges.Count; i +=3) {
            var start = Ranges[i] - (Inverted ? diff: 0);
            if (start > pos) break;
            int oldSize = Ranges[i + oldIndex], end = start + oldSize;
            if (pos <= end && i == index * 3) return true;
            diff += Ranges[i + newIndex] - oldSize;
        }
        return false;
    }

    public void ForEach(Action<int,int,int,int> f) {
        int oldIndex = Inverted ? 2 : 1, newIndex = Inverted ? 1 : 2;
        for (int i = 0, diff = 0; i < Ranges.Count; i += 3) {
            int start = Ranges[i], oldStart = start - (Inverted ? diff : 0), newStart = start + (Inverted ? 0 : diff);
            int oldSize = Ranges[i + oldIndex], newSize = Ranges[i + newIndex];
            f(oldStart, oldStart + oldSize, newStart, newStart + newSize);
            diff += newSize - oldSize;
        }
    }

    public StepMap Invert() => new(Ranges, !Inverted);

    public override string ToString() {
        return (Inverted ? "-" : "") + JsonSerializer.Serialize(Ranges);
    }

    public static StepMap Offset(int n) =>
        n == 0 ? StepMap.Empty : new(n < 0 ? new List<int>{0, -n, 0} : new List<int>{0, 0, n});

    public static StepMap Empty = new(new());
}

public class Mapping : IMappable {
    public List<StepMap> Maps { get; }
    public List<int>? Mirror { get; set; }
    public int From { get; set; }
    public int To { get; set; }

    public Mapping(List<StepMap>? maps =  null, List<int>? mirror = null, int from = 0, int? to = null) {
        Maps = maps ?? new();
        Mirror = mirror;
        From = from;
        To = to ?? Maps.Count;
    }

    public Mapping Slice(int from = 0, int? to = null) =>
        new(Maps.Slice(), Mirror?.Slice() ?? null, from, to ?? Maps.Count);

    public Mapping Copy => new(Maps.Slice(), Mirror?.Slice() ?? null, From, To);

    public void AppendMap(StepMap map, int? mirrors = null) {
        Maps.Add(map);
        To = Maps.Count;
        if (mirrors is not null) SetMirror(Maps.Count - 1, mirrors.Value);
    }

    public void AppendMapping(Mapping mapping) {
        for (int i = 0, startSize = Maps.Count; i < mapping.Maps.Count; i ++) {
            var mirr = mapping.GetMirror(i);
            AppendMap(mapping.Maps[i], mirr is not null && mirr < i ? startSize + mirr : null);
        }
    }

    public int? GetMirror(int n) {
        if (Mirror is not null) for (var i = 0; i < Mirror.Count; i ++)
            if (Mirror[i] == n) return Mirror[i + (i % 2 > 0 ? -1 : 1)];
        return null;
    }

    public void SetMirror(int n, int m) {
        Mirror ??= new();
        Mirror.Add(n); Mirror.Add(m);
    }

    public void AppendMappingInverted(Mapping mapping) {
        for (int i = mapping.Maps.Count - 1, totalSize = Maps.Count + mapping.Maps.Count; i >= 0; i--) {
            var mirr = mapping.GetMirror(i);
            AppendMap(mapping.Maps[i].Invert(), mirr is not null && mirr > i ? totalSize - mirr - 1 : null);
        }
    }

    public Mapping Invert() {
        var inverse = new Mapping();
        inverse.AppendMappingInverted(this);
        return inverse;
    }

    public int Map(int pos, int assoc = 1) {
        if (Mirror is not null) return (int)_map(pos, assoc, true).Value;
        for (var i = From; i < To; i++)
            pos = Maps[i].Map(pos, assoc);
        return pos;
    }

    public MapResult MapResult(int pos, int assoc = 1) =>
        (MapResult)_map(pos, assoc, false).Value;


    public OneOf<int, MapResult> _map(int pos, int assoc, bool simple) {
        var delInfo = 0;

        for (var i = From; i < To; i++) {
            var map = Maps[i];
            var result = map.MapResult(pos, assoc);
            if (result.Recover is not null) {
                var corr = GetMirror(i);
                if (corr is not null && corr > i && corr < To) {
                    i = corr.Value;
                    pos = Maps[corr.Value].Recover(result.Recover.Value);
                    continue;
                }
            }

            delInfo |= result.DelInfo;
            pos = result.Pos;
        }

        return simple ? pos : new MapResult(pos, delInfo, null);
    }

}