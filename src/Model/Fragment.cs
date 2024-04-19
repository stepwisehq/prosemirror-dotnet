using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;

using DotNext;

using StepWise.Prose.Collections;


namespace StepWise.Prose.Model;


internal static class ContentLikeBuilder {
	internal static ContentLike Create(ReadOnlySpan<Node> nodes) => new ContentLike { Content = nodes.ToArray().ToList() };
}

[CollectionBuilder(typeof(ContentLikeBuilder), "Create")]
public class ContentLike : IEnumerable<Node> {
    public required object Content { get; init; }

    public static implicit operator ContentLike(List<Node> nodes) => new() { Content = nodes };
    public static implicit operator ContentLike(Node node) => new() { Content = node };
    public static implicit operator ContentLike(Fragment fragment) => new() { Content = fragment };

    public IEnumerator<Node> GetEnumerator() => (IEnumerator<Node>)(IEnumerable)this.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() {
        if (Content is Node[] stuff)
            return stuff.GetEnumerator();
        else
            throw new Exception("This content is not a node list!");
    }
}

public class Fragment {
    public List<Node> Content { get; init; }
    public int Size { get; init; }

    public Fragment(List<Node> content, int? size = null) {
        Content = content;
        Size = size ?? 0;
        if (size is null) {
            foreach (var node in content)
                Size += node.NodeSize;
        }
    }

    public void NodesBetween(
        int from,
        int to,
        Func<Node, int, Node?, int, bool> f,
        int nodeStart = 0,
        Node? parent = null)
    {
        for (int i = 0, pos = 0; pos < to; i++) {
            var child = Content[i];
            var end = pos + child.NodeSize;
            if (end > from && f(child, pos + nodeStart, parent, i)) {
                var start = pos + 1;
                child.NodesBetween(
                    Math.Max(0, from - start),
                    Math.Min(child.Content.Size, to - start),
                    f, nodeStart + start);
            }
            pos = end;
        }
    }

    public void Descendants(Func<Node, int, Node?, int, bool> f) {
        NodesBetween(0, Size, f);
    }

    public string TextBetween(int from, int to, string? blockSeparator, string leafText) =>
        TextBetween(from, to, blockSeparator, _ => leafText);

    public string TextBetween(int from, int to, string? blockSeparator = null, Func<Node, string>? leafText = null) {
        var estimatedStringSize = 0;
        NodesBetween(from, to, (node, pos, _, _) => {
            estimatedStringSize += node.NodeSize;
            return true;
        });

        var text = new StringBuilder(estimatedStringSize);
        var separated = true;

        NodesBetween(from, to, (node, pos, _, _) => {
            if (node.IsText) {
                var start = Math.Max(from, pos) - pos;
                var end = Math.Min(node.Text!.Length, to - pos - start);
                var str = node.Text!.AsSpan(start, end);
                text.Append(str);
                separated = blockSeparator is null;
            } else if (node.IsLeaf) {
                if (leafText is not null) {
                    var str = leafText(node);
                    text.Append(str);
                } else if (node.Type.Spec.LeafText is not null) {
                    var str = node.Type.Spec.LeafText(node);
                    text.Append(str);
                }
                separated = blockSeparator is null;
            } else if (!separated && node.IsBlock) {
                text.Append(blockSeparator);
                separated = true;
            }
            return true;
        }, 0);
        return text.ToString();
    }

    public Fragment Append(Fragment other) {
        if (other.Size == 0) return this;
        if (Size == 0) return other;
        Node? last = LastChild, first = other.FirstChild;
        if (last is null || first is null ) throw new Exception("Unexpected null node");
        var content = Content.ToList();
        var i = 0;
        if (last.IsText && last is TextNode textNode && last.SameMarkup(first)) {
            content[^1] = textNode.WithText(last.Text + first.Text);
            i = 1;
        }
        for (;i < other.Content.Count; i++) content.Add(other.Content[i]);
        return new Fragment(content, Size + other.Size);
    }

    public Fragment Cut(int from, int? _to = null) {
        var to = _to ?? Size;
        var result = new List<Node>();
        var size = 0;
        if (to > from) for (int i = 0, pos = 0; pos < to; i++) {
            var child = Content[i];
            var end = pos + child.NodeSize;
            if (end > from) {
                if (pos < from || end > to) {
                    if (child.IsText && child is TextNode textNode)
                        child = textNode.Cut(Math.Max(0, from - pos), Math.Min(textNode.Text.Length, to - pos));
                    else
                        child = child.Cut(Math.Max(0, from - pos - 1), Math.Min(child.Content.Size, to - pos - 1));
                }
                result.Add(child);
                size += child.NodeSize;
            }
            pos = end;
        }
        return new Fragment(result, size);
    }

    public Fragment CutByIndex(int from, int to) {
        if (from == to) return Fragment.Empty;
        if (from == 0 && to == Content.Count) return this;
        return new Fragment(Content.slice(from, to));
    }

    public Fragment ReplaceChild(int index, Node node) {
        var current = Content[index];
        if (ReferenceEquals(current, node)) return this;
        var copy = Content.ToList();
        var size = Size + node.NodeSize - current.NodeSize;
        copy[index] = node;
        return new Fragment(copy, size);
    }

    public Fragment AddToStart(Node node) {
        var content = new List<Node> {node};
        content.AddRange(Content);
        return new Fragment(content, Size + node.NodeSize);
    }

    public Fragment AddToEnd(Node node) {
        var content = Content.ToList();
        content.Add(node);
        return new Fragment(content, Size + node.NodeSize);
    }

    public bool Eq(Fragment other) {
        if (Content.Count != other.Content.Count) return false;
        for (var i = 0; i < Content.Count; i++)
            if (!Content[i].Eq(other.Content[i])) return false;
        return true;
    }

    public Node? FirstChild => Content.Count > 0 ? Content[0] : null;

    public Node? LastChild => Content.Count > 0 ? Content[^1] : null;

    public int ChildCount => Content.Count;

    public Node Child(int index) {
        // Prose-model throws on out of range so we let it happen here.
        return Content[index];
    }

    public Node? MaybeChild(int index) {
        return index >= 0 && index < Content.Count ? Content[index] : null;
    }

    public void ForEach(Action<Node, int, int> f) {
        for (int i = 0, p = 0; i < Content.Count; i++) {
            var child = Content[i];
            f(child, p, i);
            p += child.NodeSize;
        }
    }

    public int? FindDiffStart(Fragment other, int pos = 0) {
        return FindDiffStart(this, other, pos);
    }

    public (int a, int b)? FindDiffEnd(Fragment other, int? pos = null, int? otherPos = null) {
        var _pos = pos ?? Size;
        var _otherPos = otherPos ?? other.Size;
        return FindDiffEnd(this, other, _pos, _otherPos);
    }

    public RetIndex FindIndex(int pos, int round = -1) {
        if (pos == 0) return new RetIndex(0, pos);
        if (pos == Size) return new RetIndex(Content.Count, pos);
        if (pos > Size || pos < 0) throw new Exception($"Position ${pos} outside of fragment ({this})");
        for (int i = 0, curPos = 0;; i++) {
            var cur = Child(i);
            var end = curPos + cur.NodeSize;
            if (end >= pos) {
                if (end == pos || round > 0) return new RetIndex(i + 1, end);
                return new RetIndex(i, curPos);
            }
            curPos = end;
        }
    }

    public override string ToString() => $"<{ToStringInner()}>";

    public string ToStringInner() => string.Join(", ", Content.Select(n => n.ToString()));

    public List<NodeDto>? ToJSON() {
        return Content.Count > 0 ? Content.Select(n => n.ToJSON()).ToList() : null;
    }

    public static Fragment FromJSON(Schema schema, List<NodeDto>? value) {
        if (value is null) return Fragment.Empty;
        return new(value.Select(schema.NodeFromJSON).ToList());
    }

    public static Fragment FromArray(List<Node> array) {
        if (array.Count == 0) return Empty;
        List<Node>? joined = new();
        var size = 0;
        for (var i = 0; i < array.Count; i++) {
            var node = array[i];
            size += node.NodeSize;
            if (i > 0 && node.IsText
                && node is TextNode textNode
                && array[i - 1].SameMarkup(node)
                && array[i - 1] is TextNode prevTextNode)
            {
                joined ??= array.slice(0, i);
                joined[^1] = textNode.WithText(prevTextNode.Text + textNode.Text);
            } else {
                joined?.Add(node);
            }
        }
        return new Fragment(joined ?? array, size);
    }

    public static Fragment From(ContentLike? content) {
        if (content is null) return Empty;
        return content.Content switch {
            Fragment fragment => fragment,
            Node node => From(node),
            List<Node> nodes => From(nodes),
            null => Empty,
            _ => throw new Exception($"Can't convert {content} to a fragment")
        };
    }

    public static Fragment From(Fragment? fragment) => fragment ?? Empty;
    public static Fragment From(List<Node>? nodes) => nodes is null ? Empty : FromArray(nodes);
    public static Fragment From(Node? node) => node is null ? Empty : new([node], node.NodeSize);


    public static int? FindDiffStart(Fragment a, Fragment b, int pos) {
        for (var i = 0;; i++) {
            if (i == a.ChildCount || i == b.ChildCount)
                return a.ChildCount == b.ChildCount ? null : pos;

            Node childA = a.Child(i), childB = b.Child(i);
            if (ReferenceEquals(childA, childB)) { pos += childA.NodeSize; continue; }

            if (!childA.SameMarkup(childB)) return pos;

            if (childA.IsText && childA is TextNode textChildA && childA.Text != childB.Text) {
                if (childB is TextNode textChildB) {
                    var minLength = Math.Min(textChildA.Text.Length, textChildB.Text.Length);
                    for (var j = 0; j < minLength && textChildA.Text[j] == textChildB.Text[j]; j++) pos++;
                }
                return pos;
            }

            if (childA.Content.Size > 0 || childB.Content.Size > 0) {
                var inner = FindDiffStart(childA.Content, childB.Content, pos + 1);
                if (inner is not null) return inner;
            }
            pos += childA.NodeSize;
        }
    }

    public static (int a, int b)? FindDiffEnd(Fragment a, Fragment b, int posA, int posB) {
        for (int iA = a.ChildCount, iB = b.ChildCount;;) {
            if (iA == 0 || iB == 0)
                return iA == iB ? null : (posA, posB);

            Node childA = a.Child(--iA), childB = b.Child(--iB);
            var size = childA.NodeSize;
            if (ReferenceEquals(childA, childB)) {
                posA -= size; posB -= size;
                continue;
            }

            if (!childA.SameMarkup(childB)) return (posA, posB);

            if (childA.IsText && childA is TextNode textChildA && childA.Text != childB.Text) {
                if (childB is TextNode textChildB) {
                    int same = 0, minSize = Math.Min(textChildA.Text.Length, textChildB.Text.Length);
                    while (same < minSize && textChildA.Text[textChildA.Text.Length - same - 1] == textChildB.Text[textChildB.Text.Length - same -1]) {
                        same++; posA--; posB--;
                    }
                }
                return (posA, posB);
            }

            if (childA.Content.Size > 0 || childB.Content.Size > 0) {
                var inner = FindDiffEnd(childA.Content, childB.Content, posA - 1, posB - 1);
                if (inner is not null) return inner;
            }
            posA -= size; posB -= size;
        }
    }

    public static Fragment Empty {get;} = new(new List<Node>());
}

public record struct RetIndex(int Index, int Offset) {}