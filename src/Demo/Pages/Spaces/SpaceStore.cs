using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorSpaces
{
    public class SpaceStore
    {
        private static SpaceStore instance;
        public static SpaceStore Instance(IJSRuntime JS)
        {
            if (instance == null)
            {
                instance = new SpaceStore(JS);
            }
            return instance;
        }

        private IJSRuntime JS;

        public SpaceStore(IJSRuntime JS)
        {
            this.JS = JS;
        }

        private static SpaceDefinition spaceDefaults = new();

        private static IEnumerable<AnchorType> AnchorTypes =>
            new[] {
            AnchorType.Left,
            AnchorType.Top,
            AnchorType.Right,
            AnchorType.Bottom
            };

        public static List<SpaceDefinition> spaceDefinitions { get; set; } = new();

        public void SetSpaces(IEnumerable<SpaceDefinition> newSpaces)
        {
            spaceDefinitions = newSpaces.ToList();
        }

        public SpaceDefinition GetSpace(string id)
        {
            return spaceDefinitions.FirstOrDefault(s => s.Id == id);
        }

        public async Task RecalcSpaces(SpaceDefinition parent)
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

                return result.Concat(spaces.Where(s => s.Type != SpaceType.Anchored)).ToArray();
            }

            IEnumerable<AnchorUpdate> anchorUpdates(SpaceDefinition space)
                => new[] {
                    new AnchorUpdate { Anchor = AnchorType.Left, Update = space.AdjustLeft },
                    new AnchorUpdate { Anchor = AnchorType.Top, Update = space.AdjustTop },
                    new AnchorUpdate { Anchor = AnchorType.Right, Update = space.AdjustRight },
                    new AnchorUpdate { Anchor = AnchorType.Bottom, Update = space.AdjustBottom }
                };

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
                        var anchoredSpaces = anchoredChildren(orderedSpaces, info.Anchor, space.ZIndex);

                        foreach (var anchoredSpace in anchoredSpaces)
                        {
                            if (anchoredSpace.Orientation == Orientation.Vertical)
                            {
                                if (anchoredSpace.Height.Size != null)
                                {
                                    adjusted.Add(anchoredSpace.Height.Size);
                                }
                                if (anchoredSpace.Height.Resized != 0)
                                {
                                    adjusted.Add($"{anchoredSpace.Height.Resized}px");
                                }
                            }
                            else
                            {
                                if (anchoredSpace.Width.Size != null)
                                {
                                    adjusted.Add(anchoredSpace.Width.Size);
                                }
                                if (anchoredSpace.Width.Resized != 0)
                                {
                                    adjusted.Add($"{anchoredSpace.Width.Resized}px");
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
                            if (anchoredSpace.Height.Resized != 0)
                            {
                                adjusted.Add($"{anchoredSpace.Height.Resized}px");
                            }
                        }
                        else
                        {
                            if (anchoredSpace.Width.Size != null)
                            {
                                adjusted.Add(anchoredSpace.Width.Size);
                            }
                            if (anchoredSpace.Width.Resized != 0)
                            {
                                adjusted.Add($"{anchoredSpace.Width.Resized}px");
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
                    await UpdateStyleDefinition(space);
                }
            }
        }

        private static string styleDefinition(SpaceDefinition space)
        {
            string getSizeString(string size)
                => int.TryParse(size, out var num) ? $"{num}px" : size;

            string css(SizeInfo size, bool dontAddCalc = false)
            {
                if (size.Size == "0" && !size.Adjusted.Any() && size.Resized == 0)
                {
                    return "0";
                }

                List<string> parts = new();
                if (size.Size != null)
                {
                    parts.Add(getSizeString(size.Size));
                }

                foreach (var adjustment in size.Adjusted)
                {
                    parts.Add(getSizeString(adjustment));
                }

                if (size.Resized != 0)
                {
                    parts.Add($"{size.Resized}px");
                }

                if (!parts.Any())
                {
                    return null;
                }

                if (parts.Count() == 1)
                {
                    return parts[0];
                }

                if (dontAddCalc)
                {
                    return string.Join(" + ", parts);
                }

                return $"calc({string.Join(" + ", parts)})";
            }

            List<string> cssElements = new();

            var style = new
            {
                position = space.Position,
                left = css(space.Left),
                top = css(space.Top),
                right = css(space.Right),
                bottom = css(space.Bottom),
                width = css(space.Width),
                height = css(space.Height),
                zIndex = space.ZIndex
            };

            List<string> cssString = new();

            if (space.Scrollable)
            {
                cssString.Add("overflow: auto;");
                cssString.Add("touch-action: auto;");
            }
            if (style.position != null)
            {
                cssString.Add($"position: {style.position};");
            }
            if (style.left != null)
            {
                cssString.Add($"left: {style.left};");
            }
            if (style.top != null)
            {
                cssString.Add($"top: {style.top};");
            }
            if (style.right != null)
            {
                cssString.Add($"right: {style.right};");
            }
            if (style.bottom != null)
            {
                cssString.Add($"bottom: {style.bottom};");
            }
            if (style.width != null)
            {
                cssString.Add($"width: {style.width};");
            }
            if (style.height != null)
            {
                cssString.Add($"height: {style.height};");
            }
            if (style.zIndex != 0)
            {
                cssString.Add($"z-index: {style.zIndex};");
            }

            if (cssString.Any())
            {
                cssElements.Add($"#{space.Id} {{ {string.Join(" ", cssString)} }}");
            }

            return string.Join(" ", cssElements);
        }

        public async Task UpdateStyleDefinition(SpaceDefinition space)
        {
            var definition = styleDefinition(space);
            await JS.InvokeVoidAsync("spaces_updateStyleDefinition", space.Id, definition);
        }

        public async Task RemoveStyleDefinition(SpaceDefinition space)
        {
            await JS.InvokeVoidAsync("spaces_removeStyleDefinition", space.Id);
        }

        public async Task AddSpace(SpaceDefinition space)
        {
            spaceDefinitions.Add(space);

            if (space.ParentId != null)
            {
                var parentSpace = GetSpace(space.ParentId);
                if (parentSpace != null)
                {
                    parentSpace.Children.Add(space);
                    await RecalcSpaces(parentSpace);
                }
            }

            await UpdateStyleDefinition(space);
        }

        public async Task RemoveSpace(SpaceDefinition space)
        {
            SetSpaces(spaceDefinitions.Where(x => x.Id != space.Id));

            if (space.ParentId != null)
            {
                var parentSpace = GetSpace(space.ParentId);
                if (parentSpace != null)
                {
                    parentSpace.Children = parentSpace.Children.Where(x => x.Id != space.Id).ToList();
                    await RecalcSpaces(parentSpace);
                }
            }

            await RemoveStyleDefinition(space);
        }

        public async Task UpdateStyles(SpaceDefinition space)
        {
            if (space.ParentId != null)
            {
                var parentSpace = GetSpace(space.ParentId);
                if (parentSpace != null)
                {
                    await RecalcSpaces(parentSpace);
                }
            }

            await UpdateStyleDefinition(space);
        }

        private static Orientation getOrientation(AnchorType? anchor) =>
            anchor == AnchorType.Bottom || anchor == AnchorType.Top ? Orientation.Vertical : Orientation.Horizontal;

        private static string getPosition(SpaceType type)
        {
            if (type == SpaceType.ViewPort)
            {
                return "fixed";
            }
            if (type == SpaceType.Fixed)
            {
                return "relative";
            }
            return "absolute";
        }

        public async Task UpdateSpace(SpaceDefinition space, SpaceProps props)
        {
            var canResizeLeft = props.Position != null && props.Position.RightResizable ? true : false;
            var canResizeRight = props.Position != null && props.Position.LeftResizable ? true : false;
            var canResizeTop = props.Position != null && props.Position.BottomResizable ? true : false;
            var canResizeBottom = props.Position != null && props.Position.TopResizable ? true : false;

            var changed = false;

            if (space.Type != props.Type)
            {
                space.Type = props.Type;
                space.Position = getPosition(props.Type);
                changed = true;
            }

            if (space.Anchor != props.Anchor)
            {
                space.Anchor = props.Anchor;
                space.Orientation = getOrientation(props.Anchor);
                changed = true;

                if (props.Type == SpaceType.Anchored)
                {
                    if (props.Anchor == AnchorType.Left)
                    {
                        space.AdjustEdge = space.AdjustLeft;
                    }
                    else if (props.Anchor == AnchorType.Top)
                    {
                        space.AdjustEdge = space.AdjustTop;
                    }
                    else if (props.Anchor == AnchorType.Right)
                    {
                        space.AdjustEdge = space.AdjustRight;
                    }
                    else if (props.Anchor == AnchorType.Bottom)
                    {
                        space.AdjustEdge = space.AdjustBottom;
                    }
                }
            }

            if (space.Left.Size != props.Position?.Left)
            {
                space.Left.Size = props.Position?.Left;
                space.Left.Resized = 0;
                changed = true;
            }

            if (space.Right.Size != props.Position?.Right)
            {
                space.Right.Size = props.Position?.Right;
                space.Right.Resized = 0;
                changed = true;
            }

            if (space.Top.Size != props.Position?.Top)
            {
                space.Top.Size = props.Position?.Top;
                space.Top.Resized = 0;
                changed = true;
            }

            if (space.Bottom.Size != props.Position?.Bottom)
            {
                space.Bottom.Size = props.Position?.Bottom;
                space.Bottom.Resized = 0;
                changed = true;
            }

            if (space.Width.Size != props.Position?.Width)
            {
                space.Width.Size = props.Position?.Width;
                space.Width.Resized = 0;
                changed = true;
            }

            if (space.Height.Size != props.Position?.Height)
            {
                space.Height.Size = props.Position?.Height;
                space.Height.Resized = 0;
                changed = true;
            }

            if (space.Order != props.Order)
            {
                space.Order = props.Order;
                changed = true;
            }

            if (space.ZIndex != (props.ZIndex ?? 0))
            {
                space.ZIndex = props.ZIndex ?? 0;
                changed = true;
            }

            if (space.Scrollable != (props.Scrollable ?? false))
            {
                space.Scrollable = props.Scrollable ?? false;
                changed = true;
            }

            if (space.MinimumSize != props.MinimumSize)
            {
                space.MinimumSize = props.MinimumSize;
                changed = true;
            }

            if (space.MaximumSize != props.MaximumSize)
            {
                space.MaximumSize = props.MaximumSize;
                changed = true;
            }

            if (space.CenterContent != (props.CenterContent ?? CenterType.None))
            {
                space.CenterContent = props.CenterContent ?? CenterType.None;
                changed = true;
            }

            if (space.HandleSize != props.HandleSize)
            {
                space.HandleSize = props.HandleSize ?? spaceDefaults.HandleSize;
                changed = true;
            }

            if (space.TouchHandleSize != props.TouchHandleSize)
            {
                space.TouchHandleSize = props.TouchHandleSize ?? spaceDefaults.TouchHandleSize;
            }

            if (space.HandlePlacement != props.HandlePlacement)
            {
                space.HandlePlacement = props.HandlePlacement ?? spaceDefaults.HandlePlacement;
                changed = true;
            }

            if (space.CanResizeBottom != canResizeBottom)
            {
                space.CanResizeBottom = canResizeBottom;
                changed = true;
            }

            if (space.CanResizeTop != canResizeTop)
            {
                space.CanResizeTop = canResizeTop;
                changed = true;
            }

            if (space.CanResizeLeft != canResizeLeft)
            {
                space.CanResizeLeft = canResizeLeft;
                changed = true;
            }

            if (space.CanResizeRight != canResizeRight)
            {
                space.CanResizeRight = canResizeRight;
                changed = true;
            }

            if (changed)
            {
                if (space.ParentId != null)
                {
                    var parentSpace = GetSpace(space.ParentId);
                    if (parentSpace != null)
                    {
                        await RecalcSpaces(parentSpace);
                    }
                }
                await UpdateStyleDefinition(space);
            }
        }

        public SpaceDefinition CreateSpace(string parentId, SpaceProps props, Action update)
        {
            var canResizeLeft = props.Position != null && props.Position.RightResizable ? true : false;
            var canResizeRight = props.Position != null && props.Position.LeftResizable ? true : false;
            var canResizeTop = props.Position != null && props.Position.BottomResizable ? true : false;
            var canResizeBottom = props.Position != null && props.Position.TopResizable ? true : false;

            var newSpace = new SpaceDefinition();
            newSpace.Id = props.Id;
            newSpace.Order = props.Order ?? newSpace.Order;
            newSpace.HandleSize = props.HandleSize ?? newSpace.HandleSize;
            newSpace.HandlePlacement = props.HandlePlacement ?? newSpace.HandlePlacement;
            newSpace.TouchHandleSize = props.TouchHandleSize ?? newSpace.TouchHandleSize;
            newSpace.MinimumSize = props.MinimumSize ?? newSpace.MinimumSize;
            newSpace.MaximumSize = props.MaximumSize ?? newSpace.MaximumSize;
            newSpace.OnResizeStart = props.OnResizeStart ?? newSpace.OnResizeStart;
            newSpace.OnResizeEnd = props.OnResizeEnd ?? newSpace.OnResizeEnd;

            newSpace.Update = update;
            newSpace.ParentId = parentId;
            newSpace.Anchor = props.Anchor;
            newSpace.Type = props.Type;
            newSpace.Orientation = getOrientation(props.Anchor);
            newSpace.Position = getPosition(props.Type);
            newSpace.Left = new SizeInfo(props.Position?.Left);
            newSpace.Right = new SizeInfo(props.Position?.Right);
            newSpace.Top = new SizeInfo(props.Position?.Top);
            newSpace.Bottom = new SizeInfo(props.Position?.Bottom);
            newSpace.Width = new SizeInfo(props.Position?.Width);
            newSpace.Height = new SizeInfo(props.Position?.Height);
            newSpace.CanResizeLeft = canResizeLeft;
            newSpace.CanResizeRight = canResizeRight;
            newSpace.CanResizeTop = canResizeTop;
            newSpace.CanResizeBottom = canResizeBottom;

            if (props.Type == SpaceType.Anchored)
            {
                if (props.Anchor == AnchorType.Left)
                {
                    newSpace.AdjustEdge = newSpace.AdjustLeft;
                }
                else if (props.Anchor == AnchorType.Top)
                {
                    newSpace.AdjustEdge = newSpace.AdjustTop;
                }
                else if (props.Anchor == AnchorType.Right)
                {
                    newSpace.AdjustEdge = newSpace.AdjustRight;
                }
                else if (props.Anchor == AnchorType.Bottom)
                {
                    newSpace.AdjustEdge = newSpace.AdjustBottom;
                }
            }

            return newSpace;
        }

        public void StartMouseResize()
        {

        }

        public void StartTouchResize()
        {

        }

        public void StartMouseDrag()
        {

        }
    }

    public class Position
    {
        public string Left { get; set; }
        public string Top { get; set; }
        public string Right { get; set; }
        public string Bottom { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
    }

    public class PositionalProps : Position
    {
        public bool LeftResizable { get; set; }
        public bool TopResizable { get; set; }
        public bool RightResizable { get; set; }
        public bool BottomResizable { get; set; }
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
        public SpaceStore Store { get; set; }
        public Action Update { get; set; }
        public Func<IEnumerable<string>, bool> AdjustEdge { get; set; }
        public Func<bool> OnResizeStart { get; set; }
        public Action<int, DOMRect> OnResizeEnd { get; set; }
        public string Id { get; set; } = "";
        public SpaceType Type { get; set; }
        public AnchorType? Anchor { get; set; }
        public Orientation Orientation { get; set; }
        public bool Scrollable { get; set; } = false;
        public int? Order { get; set; }
        public string Position { get; set; }
        public List<SpaceDefinition> Children { get; set; } = new();
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

        private bool adjustmentsEqual(IEnumerable<string> item1, IEnumerable<string> item2)
        {
            return (string.Join(",", item1) == string.Join(",", item2));
        }

        public void UpdateParent()
        {
            if (ParentId != null)
            {
                var parentSpace = Store.GetSpace(ParentId);
                if (parentSpace != null)
                {
                    parentSpace.Update();
                }
            }
        }

        public bool AdjustLeft(IEnumerable<string> adjusted)
        {
            if (adjustmentsEqual(Left.Adjusted, adjusted))
            {
                return false;
            }

            Left.Adjusted = adjusted;
            return true;
        }

        public bool AdjustRight(IEnumerable<string> adjusted)
        {
            if (adjustmentsEqual(Right.Adjusted, adjusted))
            {
                return false;
            }

            Right.Adjusted = adjusted;
            return true;
        }

        public bool AdjustTop(IEnumerable<string> adjusted)
        {
            if (adjustmentsEqual(Top.Adjusted, adjusted))
            {
                return false;
            }

            Top.Adjusted = adjusted;
            return true;
        }

        public bool AdjustBottom(IEnumerable<string> adjusted)
        {
            if (adjustmentsEqual(Bottom.Adjusted, adjusted))
            {
                return false;
            }

            Bottom.Adjusted = adjusted;
            return true;
        }
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
        public SizeInfo()
        {
        }

        public SizeInfo(string size) : this()
        {
            Size = size;
        }

        public string Size { get; set; }
        public int Resized { get; set; } = 0;
        public IEnumerable<string> Adjusted { get; set; } = Enumerable.Empty<string>();
    }

    public class CommonSpaceProps : ComponentBase
    {
        [Parameter]
        public string Id { get; set; }
        [Parameter]
        public string ClassName { get; set; }
        [Parameter]
        public string Style { get; set; }
        [Parameter]
        public string As { get; set; }
        [Parameter]
        public CenterType? CenterContent { get; set; }
        [Parameter]
        public int? ZIndex { get; set; }
        [Parameter]
        public bool? Scrollable { get; set; }
        [Parameter]
        public bool? TrackSize { get; set; }
    }

    public class SpaceProps : CommonSpaceProps
    {
        [Parameter]
        public SpaceType Type { get; set; }
        [Parameter]
        public int? Order { get; set; }
        [Parameter]
        public AnchorType Anchor { get; set; }
        [Parameter]
        public PositionalProps Position { get; set; }
        [Parameter]
        public int? HandleSize { get; set; }
        [Parameter]
        public ResizeHandlePlacement? HandlePlacement { get; set; }
        [Parameter]
        public int? TouchHandleSize { get; set; }
        [Parameter]
        public int? MinimumSize { get; set; }
        [Parameter]
        public int? MaximumSize { get; set; }
        [Parameter]
        public Func<bool> OnResizeStart { get; set; }
        [Parameter]
        public Action<int, DOMRect> OnResizeEnd { get; set; }
    }

    public class AnchoredProps : CommonSpaceProps
    {
        [Parameter]
        public string Size { get; set; }
        [Parameter]
        public int? Order { get; set; }
        [Parameter]
        public bool? Resizable { get; set; }
        [Parameter]
        public int? HandleSize { get; set; }
        [Parameter]
        public ResizeHandlePlacement? HandlePlacement { get; set; }
        [Parameter]
        public int? MinimumSize { get; set; }
        [Parameter]
        public int? MaximumSize { get; set; }
        [Parameter]
        public Func<bool> OnResizeStart { get; set; }
        [Parameter]
        public Action<string, DOMRect> OnResizeEnd { get; set; }
    }

    public class SpaceComponentBase : SpaceProps
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }
    }

    public class CommonComponentBase : CommonSpaceProps
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }
    }

    public class AnchoredComponentBase : AnchoredProps
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }
    }
}