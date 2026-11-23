using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Infra.Data;

namespace OOPTextProject.TextSystem;

/// <summary>Converts between the document model and its data model (TextSave).</summary>
public static class TextMapper
{
    public static TextSave ToSave(string saveName, Root root)
    {
        return new TextSave
        {
            SaveName = saveName,
            LastModified = DateTime.Now,
            TextRoot = root.Accept(new DataVisitor())
        };
    }

    public static Root ToRoot(TextSave save)
    {
        if (save.TextRoot == null || save.TextRoot.Kind != TextNodeKinds.Root)
        {
            throw new InvalidDataException("The save does not contain a document root.");
        }

        var root = new Root(save.TextRoot.Text);
        var usedIds = new HashSet<int> { root.Id };
        FillChildren(root, save.TextRoot, usedIds);
        return root;
    }

    private static void FillChildren(Container parent, TextNodeData data, HashSet<int> usedIds)
    {
        foreach (var childData in data.Children ?? new List<TextNodeData>())
        {
            if (!usedIds.Add(childData.Id))
            {
                throw new InvalidDataException($"Duplicate element id {childData.Id} in the save.");
            }

            switch (childData.Kind)
            {
                case TextNodeKinds.Heading:
                    var heading = new Heading(childData.Id, childData.Text);
                    parent.AddChild(heading);
                    FillChildren(heading, childData, usedIds);
                    break;
                case TextNodeKinds.Paragraph:
                    parent.AddChild(new Paragraph(childData.Id, childData.Text));
                    break;
                default:
                    throw new InvalidDataException($"Unknown element kind '{childData.Kind}'.");
            }
        }
    }

    /// <summary>
    /// VISITOR: converts an element and everything below it to data. It replaces the old <c>switch</c>
    /// over the element types.
    /// </summary>
    private sealed class DataVisitor : ITextVisitor<TextNodeData>
    {
        public TextNodeData VisitRoot(Root root) =>
            new TextNodeData(root.Id, TextNodeKinds.Root, root.Name, VisitChildren(root));

        public TextNodeData VisitHeading(Heading heading) =>
            new TextNodeData(heading.Id, TextNodeKinds.Heading, heading.Name, VisitChildren(heading));

        public TextNodeData VisitParagraph(Paragraph paragraph) =>
            new TextNodeData(paragraph.Id, TextNodeKinds.Paragraph, paragraph.Content);

        private List<TextNodeData> VisitChildren(Container container) =>
            container.Children.Select(child => child.Accept(this)).ToList();
    }
}