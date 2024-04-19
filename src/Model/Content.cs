using System.Text.RegularExpressions;

using OneOf;
using OneOf.Types;


namespace StepWise.Prose.Model;

public record MatchEdge(NodeType Type, ContentMatch Next);

public class ContentMatch {
    public static ContentMatch Empty { get; } = new(true);

    public List<MatchEdge> Next { get; init; } = new();
    public List<OneOf<NodeType, List<NodeType>, None>> WrapCache { get; init; } = new();
    public bool ValidEnd { get; init; }

    public ContentMatch(bool validEnd) {
        ValidEnd = validEnd;
    }

    public static ContentMatch Parse(string _string, Dictionary<string, NodeType> nodeTypes) {
        var stream = new TokenStream(_string, nodeTypes);
        if (stream.Next is null) return ContentMatch.Empty;
        var exp = Expression.Parse(stream);
        if (stream.Next is not null) stream.Err("Unexpected trailing text");
        var match = Expression.Dfa(Expression.Nfa(exp));
        Expression.CheckForDeads(match, stream);
        return match;
    }

    public ContentMatch? MatchType(NodeType type) {
        for (var i = 0; i < Next.Count; i++)
            if (Next[i].Type == type) return Next[i].Next;
        return null;
    }

    public ContentMatch? MatchFragment(Fragment frag, int start = 0, int? end = null) {
        end ??= frag.ChildCount;
        var cur = this;
        for (var i = start; cur is not null && i < end; i++)
            cur = cur.MatchType(frag.Child(i).Type);
        return cur;
    }

    public bool InlineContent =>
        // Prose-Model does not protect against null Next.First()
        Next.Count != 0 && Next[0].Type.IsInline;

    public NodeType? DefaultType {
        get {
            for (var i = 0; i < Next.Count; i++) {
                var (Type, _) = Next[i];
                if (!(Type.IsText || Type.HasRequiredAttrs())) return Type;
            }
            return null;
        }
    }

    public bool Compatible(ContentMatch other) {
        for (var i = 0; i < Next.Count; i++)
            for (var j = 0; j < other.Next.Count; j++)
                if (Next[i].Type == other.Next[j].Type) return true;
        return false;
    }

    public Fragment? FillBefore(Fragment after, bool toEnd = false, int startIndex = 0) {
        List<ContentMatch> seen = new() {this};

        Fragment? search(ContentMatch match, List<NodeType> types) {
            var finished = match.MatchFragment(after, startIndex);
            if (finished is not null && (!toEnd || finished.ValidEnd))
                return Fragment.From(types.Select(tp => tp.CreateAndFill()!).ToList());

            for (var i = 0; i < match.Next.Count; i++) {
                var (type, next) = match.Next[i];
                if (!(type.IsText || type.HasRequiredAttrs()) && !seen.Contains(next)) {
                    seen.Add(next);
                    var found = search(next, types.Append(type).ToList());
                    if (found is not null) return found;
                }
            }
            return null;
        }
        return search(this, new());
    }

    public List<NodeType>? FindWrapping(NodeType target) {
        for (var i = 0; i < WrapCache.Count; i += 2)
            if (ReferenceEquals(WrapCache[i].Value, target)) return WrapCache[i + 1].Value is List<NodeType> list? list : null;
        var computed = ComputeWrapping(target);
        WrapCache.Add(target);
        if (computed is not null) WrapCache.Add(computed);
        else WrapCache.Add(new None());
        return computed;
    }

    public record Active(ContentMatch Match, NodeType? Type, Active? Via) {}
    public List<NodeType>? ComputeWrapping(NodeType target) {
        var seen = new Dictionary<string, bool>();
        var active = new Queue<Active>();
        active.Enqueue(new(this, null, null));
        while (active.Count > 0) {
            var current = active.Dequeue() ?? throw new Exception("Unexpected null current");
            var match = current.Match;
            if (match.MatchType(target) is not null) {
                var result = new List<NodeType>();
                for (var obj = current; obj.Type is not null; obj = obj.Via!)
                    result.Add(obj.Type);
                result.Reverse();
                return result;
            }
            for (var i = 0; i < match.Next.Count; i++) {
                var (type, next) = match.Next[i];
                if (!type.IsLeaf && !type.HasRequiredAttrs() && !seen.ContainsKey(type.Name) && (current.Type is null || next.ValidEnd)) {
                    active.Enqueue(new(type.ContentMatch, type, current));
                    seen[type.Name] = true;
                }
            }
        }
        return null;
    }
}

public partial class TokenStream {
    [GeneratedRegex(@"\s*(?=\b|\W|$)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex TokenSplitRegex();

    private readonly string _string;
    public Dictionary<string, NodeType> NodeTypes { get; }

    public bool? Inline { get; set; }
    public int Pos { get; set; }
    public List<string> Tokens { get; init; }


    public TokenStream(string _string, Dictionary<string, NodeType> nodeTypes) {
        this._string = _string;
        NodeTypes = nodeTypes;
        Tokens = TokenSplitRegex().Split(_string).Where(s => s != string.Empty).ToList();
    }

    public string? Next => Pos < Tokens.Count ? Tokens[Pos] : null;

    public bool Eat(string token) {
        if (Next == token) {
            Pos++;
            return true;
        }
        return false;
    }

    public void Err(string msg) {
        throw new SyntaxException($"{msg} (in content expression '{_string}')");
    }
}


public class SyntaxException : Exception {
    public SyntaxException() {}
    public SyntaxException(string message) : base(message) {}
    public SyntaxException(string message, Exception inner) : base(message, inner) {}
}

public record Expression {
    public static Expression Parse(TokenStream stream) {
        var exprs = new List<Expression>();
        do exprs.Add(ParseSeq(stream));
        while (stream.Eat("|"));
        return exprs.Count == 1 ? exprs.First() : new Choice(exprs);
    }

    public static Expression ParseSeq(TokenStream stream) {
        var exprs = new List<Expression>();

        do {
            exprs.Add(ParseSubscript(stream));
        } while(stream.Next?.Length > 0 && stream.Next != ")" && stream.Next != "|");
        return exprs.Count == 1 ? exprs.First() : new Sequence(exprs);
    }

    public static Expression ParseSubscript(TokenStream stream) {
        var expr = ParseAtom(stream);
        while(true) {
            if (stream.Eat("+"))
                expr = new Plus(expr);
            else if (stream.Eat("*"))
                expr = new Star(expr);
            else if (stream.Eat("?"))
                expr = new Optional(expr);
            else if (stream.Eat("{"))
                expr = ParseRange(stream, expr);
            else
                break;
        };
        return expr;
    }

    public static int ParseNum(TokenStream stream) {
        if ( stream.Next is null || new Regex(@"\D").IsMatch(stream.Next))
            stream.Err($"Expected number, got '{stream.Next}'");
        var result = int.Parse(stream.Next!);
        stream.Pos++;
        return result;
    }

    public static Expression ParseRange(TokenStream stream, Expression expr) {
        var min = ParseNum(stream);
        var max = min;
        if (stream.Eat(",")) {
            if (stream.Next != "}") max = ParseNum(stream);
            else max = -1;
        }
        if (!stream.Eat("}")) stream.Err("Unclosed braced range");
        return new Range(min, max, expr);
    }

    public static List<NodeType> ResolveName(TokenStream stream, string name) {
        var types = stream.NodeTypes;

        if (types.TryGetValue(name, out var type))
            return new() {type};

        var result = new List<NodeType>();
        foreach (var (typeName, _type) in types) {
            if (_type.Groups.Contains(name)) result.Add(_type);
        }
        if (result.Count == 0) stream.Err($"No node type or group '{name}' found");
        return result;
    }

    public static Expression ParseAtom(TokenStream stream) {
        if (stream.Eat("(")) {
            var expr = Parse(stream);
            if (!stream.Eat(")")) stream.Err("Missing closing paren");
            return expr;
        } else if (!(new Regex(@"\W")).IsMatch(stream.Next ?? "")) {
            var exprs = ResolveName(stream, stream.Next ?? "").Select(type => {
                if (stream.Inline is null) stream.Inline = type.IsInline;
                else if (stream.Inline != type.IsInline) stream.Err("Mixing inline and block content");
                return new Name(type);
            }).ToList<Expression>();
            stream.Pos++;
            return exprs.Count == 1 ? exprs.First() : new Choice(exprs);
        } else {
            stream.Err($"Unexpected token '{stream.Next}'");
            throw new Exception(); // Statisfy compiler
        }
    }

    public static List<List<Edge>> Nfa(Expression expr) {
        List<List<Edge>> nfa = new(){new()};
        connect(compile(expr, 0), node());
        return nfa;

        int node() {
            nfa.Add(new());
            return nfa.Count - 1;
        };

        Edge edge(int from, int? to = null, NodeType? term = null) {
            var edge = new Edge(term, to);
            nfa[from].Add(edge);
            return edge;
        };

        void connect(List<Edge> edges, int to) {
            foreach (var edge in edges) edge.To = to;
        };

        List<Edge> compile(Expression _expr, int from) {
            if (_expr is Choice choice)
                return choice.exprs.Aggregate(new List<Edge>(), (outList, _expr) => outList.Concat(compile(_expr, from)).ToList());
            else if (_expr is Sequence seq) {
                for (var i = 0 ;; i++) {
                    var next = compile(seq.exprs[i], from);
                    if (i == seq.exprs.Count - 1) return next;
                    connect(next, from = node());
                }
            } else if (_expr is Star star) {
                var loop = node();
                edge(from, loop);
                connect(compile(star.expr, loop), loop);
                return [edge(loop)];
            } else if (_expr is Plus plus) {
                var loop = node();
                connect(compile(plus.expr, from), loop);
                connect(compile(plus.expr, loop), loop);
                return [edge(loop)];
            } else if (_expr is Optional option) {
                return [edge(from), ..compile(option.expr, from)];
            } else if (_expr is Range range) {
                var cur = from;
                for (var i = 0; i < range.min; i++) {
                    var next = node();
                    connect(compile(range.expr, cur), next);
                    cur = next;
                }
                if (range.max is -1) {
                   connect(compile(range.expr, cur), cur);
                } else {
                    for (var i = range.min; i < range.max; i++) {
                        var next = node();
                        edge(cur, next);
                        connect(compile(range.expr, cur), next);
                        cur = next;
                    }
                }
                return [edge(cur)];
            } else if (_expr is Name name) {
                return [edge(from, null, name.value)];
            } else {
                throw new Exception("Unknown expression type");
            }
        }
    }

    public static List<int> NullFrom(List<List<Edge>> nfa, int? node) {
        List<int> result = new();
        scan(node);
        result.Sort();
        result.Reverse();
        return result;

        void scan(int? _node) {
            if (_node is null) throw new Exception("Unexpected null node");
            var edges = nfa[_node.Value];
            if (edges.Count == 1 && edges.First().Term is null) {
                scan(edges.First().To);
                return;
            }
            result.Add(_node.Value);
            foreach (var (term, to) in edges) {
                if (to is null) throw new Exception("Unexpected null to");
                if (term is null && !result.Contains(to.Value)) scan(to);
            }
        }
    }

    public static ContentMatch Dfa(List<List<Edge>> nfa) {
        var labeled = new Dictionary<string, ContentMatch>();
        return explore(NullFrom(nfa, 0));

        ContentMatch explore(List<int> states) {
            var _out = new List<(NodeType, List<int>)>();
            foreach (var node in states) {
                foreach (var (term, to) in nfa[node]) {
                    if (term is null) continue;
                    List<int>? set = null;
                    for (var i = 0; i < _out.Count; i++) if (_out[i].Item1 == term) set = _out[i].Item2;
                    NullFrom(nfa, to).ForEach(node => {
                        if (set is null) _out.Add((term, set = new()));
                        if (!set.Contains(node)) set.Add(node);
                    });
                }
            }

            var state = new ContentMatch(states.Contains(nfa.Count - 1));
            labeled[string.Join(",", states)] = state;
            foreach (var (_type, _states) in _out) {
                _states.Sort();
                _states.Reverse();
                var label = string.Join(",", _states);
                if (!labeled.TryGetValue(label, out var labeledState))
                    labeledState = explore(_states);

                state.Next.Add(new MatchEdge(_type, labeledState));
            }
            return state;
        }
    }

    public static void CheckForDeads(ContentMatch match, TokenStream stream) {
        var work = new List<ContentMatch>{match};
        for (var i = 0; i < work.Count; i++) {
            var state = work[i]; var dead = !state.ValidEnd; var nodes = new List<string>();
            for (var j = 0; j < state.Next.Count; j++) {
                var (type, next) = state.Next[j];
                nodes.Add(type.Name);
                if (dead && !(type.IsText || type.HasRequiredAttrs())) dead = false;
                if (!work.Contains(next)) work.Add(next);
            }
            if (dead) stream.Err($"Only non-generatable nodes ({string.Join(", ", nodes)}) in a required position (see https://prosemirror.net/docs/guide/#generatable)");
        }
    }
}

public record Edge(NodeType? Term, int? To) {
    public int? To { get; set; } = To;
}

public record Choice(List<Expression> exprs) : Expression;

public record Sequence(List<Expression> exprs) : Expression;

public record Plus(Expression expr) : Expression;

public record Star(Expression expr) : Expression;

public record Optional(Expression expr) : Expression;

public record Range(int min, int max, Expression expr) : Expression;

public record Name(NodeType value) : Expression;