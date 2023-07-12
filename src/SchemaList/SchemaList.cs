using StepWise.Prose.Collections;
using StepWise.Prose.Model;


namespace StepWise.Prose.SchemaList;

/// Convenience function for adding list-related node types to a map
/// specifying the nodes for a schema. Adds
/// [`orderedList`](#schema-list.orderedList) as `"ordered_list"`,
/// [`bulletList`](#schema-list.bulletList) as `"bullet_list"`, and
/// [`listItem`](#schema-list.listItem) as `"list_item"`.
///
/// `itemContent` determines the content expression for the list items.
/// If you want the commands defined in this module to apply to your
/// list structure, it should have a shape like `"paragraph block*"` or
/// `"paragraph (ordered_list | bullet_list)*"`. `listGroup` can be
/// given to assign a group name to the list node types, for example
/// `"block"`.
public static class ListSchema {
    public static OrderedDictionary<string, NodeSpec> AddListNodes(OrderedDictionary<string, NodeSpec> nodes, string itemContent, string? listGroup = null) {
        return new(nodes) {
            ["ordered_list"] = new() {
                Attrs = new() {["order"] = new() {Default = new(1)}},
                Content = "list_item+",
                Group = listGroup,
            },
            ["bullet_list"] = new() {
                Content = "list_item+",
                Group = listGroup
            },
            ["list_item"] = new() {
                Content = itemContent,
                Defining = true
            }
        };
    }
}
