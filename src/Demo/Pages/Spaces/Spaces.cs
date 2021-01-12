using System;
using System.Collections.Generic;
using System.Linq;

public static class Spaces
{
    private static IEnumerable<AnchorType> AnchorTypes =>
        new[] {
            AnchorType.Left,
            AnchorType.Top,
            AnchorType.Right,
            AnchorType.Bottom
        };

    public static List<SpaceDefinition> spaceDefinitions { get; set; } = new();

    public static void SetSpaces(IEnumerable<SpaceDefinition> newSpaces)
    {
        spaceDefinitions = newSpaces.ToList();
    }

    public static SpaceDefinition GetSpace(string id)
    {
        return spaceDefinitions.FirstOrDefault(s => s.Id == id);
    }

    public static void RecalcSpaces(SpaceDefinition parent)
    {
        IEnumerable<SpaceDefinition> addDefaultOrders(IEnumerable<SpaceDefinition> spaces)
        {
            IEnumerable<SpaceDefinition> result = Enumerable.Empty<SpaceDefinition>();

            foreach (var anchorType in AnchorTypes)
            {
                var anchoredSpaces = spaces.Where(x => x.Anchor.HasValue && x.Anchor.Value == anchorType);
                var zIndices = anchoredSpaces.Select(x => x.ZIndex).Distinct();
                foreach (var zIndex in zIndices)
                {
                    var anchoredSpacesInLayer = anchoredSpaces.Where(x => x.ZIndex == zIndex);
                    var orderedSpaces = anchoredSpacesInLayer.Where(x => x.Order.HasValue);
                    var unorderedSpaces = anchoredSpacesInLayer.Where(x => !x.Order.HasValue);
                    var maxOrder = orderedSpaces.Max(x => x.Order);
                    result = result.Concat(
                        orderedSpaces
                    ).Concat(unorderedSpaces);
                }
            }

            return result.Concat(spaces.Where(s => !s.Anchor.HasValue)).ToArray();
        }

        IEnumerable<AnchorUpdate> anchorUpdates(SpaceDefinition space)
        {
            return new[] {
                new AnchorUpdate { Anchor = AnchorType.Left, Update = space.AdjustLeft },
                new AnchorUpdate { Anchor = AnchorType.Top, Update = space.AdjustTop },
                new AnchorUpdate { Anchor = AnchorType.Right, Update = space.AdjustRight },
                new AnchorUpdate { Anchor = AnchorType.Bottom, Update = space.AdjustBottom }
            };
        }

        IEnumerable<SpaceDefinition> anchoredChildren(IEnumerable<SpaceDefinition> spaces, AnchorType anchor, int zIndex) =>
            spaces.Where(s => s.Type == SpaceType.Anchored && s.Anchor == anchor && s.ZIndex == zIndex);

        var orderedSpaces = addDefaultOrders(parent.Children);
        foreach (var space in orderedSpaces)
        {
            var changed = false;

            if (space.Type == SpaceType.Fill)
            {
                foreach (var info in anchorUpdates(space))
                {
                    var adjusted = new List<string>();
                    var anchoredSpaces = anchoredChildren(orderedSpaces, space.Anchor.Value, space.ZIndex);

                    foreach (var anchoredSpace in anchoredSpaces)
                    {
                        if (anchoredSpace.Orientation == Orientation.Vertical)
                        {
                            if (anchoredSpace.Height.Size != null)
                            {
                                adjusted.Add(anchoredSpace.Height.Size);
                            }
                            if (anchoredSpace.Height.Resized != null)
                            {
                                adjusted.Add(anchoredSpace.Height.Resized);
                            }
                        }
                        else
                        {
                            if (anchoredSpace.Width.Size != null)
                            {
                                adjusted.Add(anchoredSpace.Width.Size);
                            }
                            if (anchoredSpace.Width.Resized != null)
                            {
                                adjusted.Add(anchoredSpace.Width.Resized);
                            }
                        }
                    }

                    if (info.Update(adjusted))
                    {
                        changed = true;
                    }
                }
            }
            else if (space.Type == SpaceType.Anchored)
            {
                var adjusted = new List<string>();
                var anchoredSpaces =
                    anchoredChildren(orderedSpaces, space.Anchor.Value, space.ZIndex)
                        .Where(s => s.Id != space.Id && s.Order <= space.Order);

                foreach (var anchoredSpace in anchoredSpaces)
                {
                    if (anchoredSpace.Orientation == Orientation.Vertical)
                    {
                        if (anchoredSpace.Height.Size != null)
                        {
                            adjusted.Add(anchoredSpace.Height.Size);
                        }
                        if (anchoredSpace.Height.Resized != null)
                        {
                            adjusted.Add(anchoredSpace.Height.Resized);
                        }
                    }
                    else
                    {
                        if (anchoredSpace.Width.Size != null)
                        {
                            adjusted.Add(anchoredSpace.Width.Size);
                        }
                        if (anchoredSpace.Width.Resized != null)
                        {
                            adjusted.Add(anchoredSpace.Width.Resized);
                        }
                    }

                    if (space.AdjustEdge(adjusted))
                    {
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                UpdateStyleDefinition(space);
            }
        }
    }

    public static void UpdateStyleDefinition(SpaceDefinition space)
    {

    }

    public static void RemoveStyleDefinition(SpaceDefinition space)
    {

    }

    public static void AddSpace(SpaceDefinition space)
    {
        spaceDefinitions.Add(space);

        if (space.ParentId != null)
        {
            var parentSpace = GetSpace(space.ParentId);
            if (parentSpace != null)
            {
                parentSpace.Children.Add(space);
                RecalcSpaces(parentSpace);
            }
        }

        UpdateStyleDefinition(space);
    }

    public static void RemoveSpace(SpaceDefinition space)
    {
        SetSpaces(spaceDefinitions.Where(x => x.Id != space.Id));

        if (space.ParentId != null)
        {
            var parentSpace = GetSpace(space.ParentId);
            if (parentSpace != null)
            {
                parentSpace.Children = parentSpace.Children.Where(x => x.Id != space.Id).ToList();
                RecalcSpaces(parentSpace);
            }
        }

        RemoveStyleDefinition(space);
    }

    public static void UpdateStyles(SpaceDefinition space)
    {
        if (space.ParentId != null)
        {
            var parentSpace = GetSpace(space.ParentId);
            if (parentSpace != null)
            {
                RecalcSpaces(parentSpace);
            }
        }

        UpdateStyleDefinition(space);
    }

    public static void UpdateSpace(SpaceDefinition space/*, properties */)
    {

    }

    public static SpaceDefinition CreateSpace(string parentId/*, properties, updateCallback */)
    {
        return null;
    }

    public static void StartMouseResize()
    {

    }

    public static void StartTouchResize()
    {

    }

    public static void StartMouseDrag()
    {

    }
}

public class AnchorUpdate
{
    public AnchorType Anchor { get; set; }
    public Func<IEnumerable<string>, bool> Update { get; set; }
}

public enum SpaceType
{
    ViewPort,
    Fixed,
    Anchored,
    Fill,
    Positioned,
    Custom
}

public enum AnchorType
{
    Left,
    Right,
    Top,
    Bottom
}

public enum Orientation
{
    Horizontal,
    Vertical
}

public enum ResizeHandlePlacement
{
    OverlayInside,
    Inside,
    OverlayBoundary
}

public enum CenterType
{
    None,
    Vertical,
    HorizontalVertical
}

public class SpaceDefinition
{
    public Func<IEnumerable<string>, bool> AdjustLeft { get; set; }
    public Func<IEnumerable<string>, bool> AdjustRight { get; set; }
    public Func<IEnumerable<string>, bool> AdjustTop { get; set; }
    public Func<IEnumerable<string>, bool> AdjustBottom { get; set; }
    public Func<IEnumerable<string>, bool> AdjustEdge { get; set; }
    public string Id { get; set; } = "";
    public SpaceType Type { get; set; }
    public AnchorType? Anchor { get; set; }
    public Orientation Orientation { get; set; }
    public bool Scrollable { get; set; } = false;
    public int? Order { get; set; }
    public string Position { get; set; }
    public List<SpaceDefinition> Children { get; set; }
    public string ParentId { get; set; }
    public SizeInfo Left { get; set; } = new();
    public SizeInfo Top { get; set; } = new();
    public SizeInfo Right { get; set; } = new();
    public SizeInfo Bottom { get; set; } = new();
    public SizeInfo Width { get; set; } = new();
    public SizeInfo Height { get; set; } = new();
    public int ZIndex { get; set; } = 0;
    public CenterType CenterContent { get; set; } = CenterType.None;
    public bool Resizing { get; set; } = false;
    public int? MinimumSize { get; set; }
    public int? MaximumSize { get; set; }
    public int HandleSize { get; set; } = 5;
    public int TouchHandleSize { get; set; } = 5;
    public ResizeHandlePlacement HandlePlacement { get; set; } = ResizeHandlePlacement.OverlayInside;
    public bool CanResizeTop { get; set; }
    public bool CanResizeLeft { get; set; }
    public bool CanResizeRight { get; set; }
    public bool CanResizeBottom { get; set; }
}

public class DOMRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class SizeInfo
{
    public string Size { get; set; }
    public string Resized { get; set; }
    public List<string> Adjusted { get; set; } = new();
}