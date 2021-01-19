using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;

namespace BlazorSpaces
{
    public class Position
    {
        public string Left { get; set; }
        public string Top { get; set; }
        public string Right { get; set; }
        public string Bottom { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
    }

    public static class PositionExtensions
    {
        public static PositionalProps Left(this PositionalProps pos, string value)
        {
            pos.Left = value;
            return pos;
        }
        public static PositionalProps Top(this PositionalProps pos, string value)
        {
            pos.Top = value;
            return pos;
        }
        public static PositionalProps Right(this PositionalProps pos, string value)
        {
            pos.Right = value;
            return pos;
        }
        public static PositionalProps Bottom(this PositionalProps pos, string value)
        {
            pos.Bottom = value;
            return pos;
        }
        public static PositionalProps LeftResizable(this PositionalProps pos)
        {
            pos.LeftResizable = true;
            return pos;
        }
        public static PositionalProps TopResizable(this PositionalProps pos)
        {
            pos.TopResizable = true;
            return pos;
        }
        public static PositionalProps RightResizable(this PositionalProps pos)
        {
            pos.RightResizable = true;
            return pos;
        }
        public static PositionalProps BottomResizable(this PositionalProps pos)
        {
            pos.BottomResizable = true;
            return pos;
        }
        public static PositionalProps Width(this PositionalProps pos, string value)
        {
            pos.Width = value;
            return pos;
        }
        public static PositionalProps Height(this PositionalProps pos, string value)
        {
            pos.Height = value;
            return pos;
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

    public enum ResizeType
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
        public ElementReference Element { get; set; }
        public string Id { get; set; } = "";
        public SpaceType Type { get; set; }
        public AnchorType? Anchor { get; set; }
        public Orientation Orientation { get; set; }
        public bool Scrollable { get; set; } = false;
        public int? Order { get; set; }
        public string Position { get; set; }
        public SpaceDefinition Parent { get; set; }
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

        public Queue<string> DeferedStyleUpdates { get; set; } = new();
        public Queue<string> DeferedStyleRemovals { get; set; } = new();

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
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
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

    public class Coords
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class PositionalProps : Position
    {
        public bool LeftResizable { get; set; }
        public bool TopResizable { get; set; }
        public bool RightResizable { get; set; }
        public bool BottomResizable { get; set; }

        public static PositionalProps Create() => new PositionalProps();
    }

    public class CommonSpaceProps : ComponentBase
    {
        [Parameter]
        public string Id { get; set; }
        [Parameter]
        public string Class { get; set; }
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