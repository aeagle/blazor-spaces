using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
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
                    return "0px";
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

            if (space.Scrollable)
            {
                cssElements.Add($"#{space.Id} > .spaces-space-inner {{ overflow: auto; touch-action: auto; }}");
            }

            var handleOffset = 0;
            var touchHandleSize = space.TouchHandleSize / 2 - space.HandleSize / 2;

            switch (space.HandlePlacement)
            {
                case ResizeHandlePlacement.Inside:
                case ResizeHandlePlacement.OverlayInside:
                    handleOffset = space.HandleSize;
                    if (space.Type == SpaceType.Positioned)
                    {
                        handleOffset = 0;
                    }
                    break;
                case ResizeHandlePlacement.OverlayBoundary:
                    handleOffset = space.HandleSize / 2;
                    break;
            }

            if (space.CanResizeLeft)
            {
                cssElements.Add($"#{space.Id}-ml {{ left: calc({css(space.Left, true)} + {css(space.Width, true)} - {handleOffset}px); width: {space.HandleSize}px; }}");
                cssElements.Add($"#{space.Id}-ml:after {{ left: -{touchHandleSize}px; right: -{touchHandleSize}px; top: 0; bottom: 0; }}");
            }
            if (space.CanResizeTop)
            {
                cssElements.Add($"#{space.Id}-mt {{ top: calc({css(space.Top, true)} + {css(space.Height, true)} - {handleOffset}px); height: {space.HandleSize}px; }}");
                cssElements.Add($"#{space.Id}-mt:after {{ top: -{touchHandleSize}px; bottom: -{touchHandleSize}px; left: 0; right: 0; }}");
            }
            if (space.CanResizeRight)
            {
                cssElements.Add($"#{space.Id}-mr {{ right: calc({css(space.Right, true)} + {css(space.Width, true)} - {handleOffset}px); width: {space.HandleSize}px; }}");
                cssElements.Add($"#{space.Id}-mr:after {{ left: -{touchHandleSize}px; right: -{touchHandleSize}px; top: 0; bottom: 0; }}");
            }
            if (space.CanResizeBottom)
            {
                cssElements.Add($"#{space.Id}-mb {{ bottom: calc({css(space.Bottom, true)} + {css(space.Height, true)} - {handleOffset}px); height: {space.HandleSize}px; }}");
                cssElements.Add($"#{space.Id}-mb:after {{ top: -{touchHandleSize}px; bottom: -{touchHandleSize}px; left: 0; right: 0; }}");
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

        public async Task StartMouseResize(ResizeType resizeType, SpaceDefinition space, MouseEventArgs e)
        {
            await CoreResizing.StartResize<MouseEventArgs>(
                JS,
                this,
                e,
                resizeType,
                space,
                "mouseup",
                "mousemove",
                (EventArgs e) => new Coords { X = (int)(e as MouseEventArgs).ClientX, Y = (int)(e as MouseEventArgs).ClientY },
                space.OnResizeEnd
            );
        }

        public async Task StartTouchResize(ResizeType resizeType, SpaceDefinition space, TouchEventArgs e)
        {
            await CoreResizing.StartResize<TouchEventArgs>(
                JS,
                this,
                e,
                resizeType,
                space,
                "touchend",
                "touchmove",
                (EventArgs e) => new Coords { X = (int)(e as TouchEventArgs).Touches[0].ClientX, Y = (int)(e as TouchEventArgs).Touches[0].ClientY },
                space.OnResizeEnd
            );
        }

        public void StartMouseDrag(ResizeType resizeType, SpaceDefinition space)
        {

        }
    }
}