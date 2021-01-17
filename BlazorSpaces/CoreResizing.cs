using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace BlazorSpaces
{
    public static class CoreResizing
    {
		private static void customSizeHoriz(SpaceDefinition space, int adjust)
		{
			if (space.Width.Size != null)
			{
				space.Width.Resized = -adjust;
			}
		}

		private static void customSizeVert(SpaceDefinition space, int adjust)
		{
			if (space.Height.Size != null)
			{
				space.Height.Resized = -adjust;
			}
		}

		private static Action<int> getCustomSizing(ResizeType resizeType, SpaceDefinition space)
		{
			if (resizeType == ResizeType.Left)
			{
				return space.Type == SpaceType.Positioned ? (int a) => { customSizeHoriz(space, a); } : null;
			}
			else if (resizeType == ResizeType.Right)
			{
				return space.Type == SpaceType.Positioned ? (space.Width.Size == null ? null : (int a) => customSizeHoriz(space, a)) : null;
			}
			else if (resizeType == ResizeType.Top)
			{
				return space.Type == SpaceType.Positioned ? (int a) => customSizeVert(space, a) : null;
			}
			else if (resizeType == ResizeType.Bottom)
			{
				return space.Type == SpaceType.Positioned ? (space.Height.Size == null ? null : (int a) => customSizeVert(space, a)) : null;
			}
			throw new Exception("unknown resize type");
		}

		private static SizeInfo getTargetSize(ResizeType resizeType, SpaceDefinition space)
		{
			if (resizeType == ResizeType.Left)
			{
				return space.Type == SpaceType.Positioned ? space.Left : space.Width;
			}
			else if (resizeType == ResizeType.Right)
			{
				return space.Type == SpaceType.Positioned ? (space.Width.Size != null ? space.Width : space.Right) : space.Width;
			}
			else if (resizeType == ResizeType.Top)
			{
				return space.Type == SpaceType.Positioned ? space.Top : space.Height;
			}
			else if (resizeType == ResizeType.Bottom)
			{
				return space.Type == SpaceType.Positioned ? (space.Height.Size != null ? space.Height : space.Bottom) : space.Height;
			}
			throw new Exception("unknown resize type");
		}

		private static ResizeType getResizeType(ResizeType resizeType, SpaceDefinition space)
		{
			if (resizeType == ResizeType.Left)
			{
				return ResizeType.Left;
			}
			else if (resizeType == ResizeType.Right)
			{
				return space.Type == SpaceType.Positioned ? (space.Width.Size != null ? ResizeType.Left : ResizeType.Right) : ResizeType.Right;
			}
			else if (resizeType == ResizeType.Top)
			{
				return ResizeType.Top;
			}
			else if (resizeType == ResizeType.Bottom)
			{
				return space.Type == SpaceType.Positioned ? (space.Height.Size != null ? ResizeType.Top : ResizeType.Bottom) : ResizeType.Bottom;
			}
			throw new Exception("unknown resize type");
		}

		private static int getCustomOriginal(ResizeType resizeType, SpaceDefinition space)
		{
			if (resizeType == ResizeType.Left)
			{
				return space.Width.Size != null ? -space.Width.Resized : 0;
			}
			else if (resizeType == ResizeType.Right)
			{
				return 0;
			}
			else if (resizeType == ResizeType.Top)
			{
				return space.Height.Size != null ? -space.Height.Resized : 0;
			}
			else if (resizeType == ResizeType.Bottom)
			{
				return 0;
			}
			throw new Exception("unknown resize type");
		}

		private static async Task onResize(
			SpaceStore store,
			SpaceDefinition space,
			SizeInfo targetSize,
			ResizeType resizeType,
			int startSize,
			Coords coords,
			int customOriginal,
			int x,
			int y,
			int minimumAdjust,
			int? maximumAdjust,
			Action<int> customAdjust)
        {
			var adjustment =
				startSize +
				(resizeType == ResizeType.Left || resizeType == ResizeType.Right
					? resizeType == ResizeType.Left
						? x - coords.X
						: coords.X - x
					: resizeType == ResizeType.Top
						? y - coords.Y
						: coords.Y - y);

			if (adjustment < minimumAdjust)
            {
				adjustment = minimumAdjust;
            } 
			else
            {
				if (maximumAdjust.HasValue)
                {
					if (adjustment > maximumAdjust)
                    {
						adjustment = maximumAdjust.Value;
                    }
                }
            }

			if (adjustment != targetSize.Resized)
            {
				targetSize.Resized = adjustment;

				customAdjust?.Invoke(adjustment + customOriginal);

				await store.UpdateStyles(space);
            }
        }

		public static async Task StartResize<T>(
			IJSRuntime JS,
			SpaceStore store,
            T e,
            ResizeType resizeHandleType,
            SpaceDefinition space,
            string EndEvent,
            string MoveEvent,
            Func<EventArgs, Coords> GetCoords,
            Action<int, DOMRect> OnResizeEnd) where T : EventArgs
        {
            if (space.OnResizeStart != null)
            {
                var result = space.OnResizeStart();
                if (!result)
                {
                    return;
                }
            }

            var originalCoords = GetCoords(e);
			var resizeType = getResizeType(resizeHandleType, space);
			var customAdjust = getCustomSizing(resizeHandleType, space);
			var targetSize = getTargetSize(resizeHandleType, space);
			var customOriginal = getCustomOriginal(resizeHandleType, space) - targetSize.Resized;

			space.Resizing = true;
			space.UpdateParent();

			var rect = /* space.element.getBoundingRect */ new DOMRect();
			var size = resizeType == ResizeType.Left || resizeType == ResizeType.Right ? rect.Width : rect.Height;
			var startSize = targetSize.Resized;
			var minimumAdjust = (space.MaximumSize ?? 20) - size + targetSize.Resized;
			var maximumAdjust = space.MaximumSize.HasValue ? space.MaximumSize - size + targetSize.Resized : null;

			var lastX = 0;
			var lastY = 0;
			var moved = false;

			async Task resize(int x, int y)
			{
				await onResize(
					store,
					space,
					targetSize,
					resizeType,
					startSize,
					originalCoords,
					customOriginal,
					x,
					y,
					minimumAdjust,
					maximumAdjust,
					customAdjust
				);
			}

			[JSInvokable("blazorSpaces_startResize")]
			Task withPreventDefault(T e)
			{
				moved = true;
				var newCoords = GetCoords(e);
				lastX = newCoords.X;
				lastY = newCoords.Y;
				// e.preventDefault();

				//throttle((x, y) => window.requestAnimationFrame(() => resize(x, y)), RESIZE_THROTTLE)(lastX, lastY);

				return Task.CompletedTask;
			};

			[JSInvokable("blazorSpaces_endResize")]
			async Task removeListener()
            {
				if (moved)
                {
					await resize(lastX, lastY);
                }

				//window.removeEventListener(moveEvent, blazorSpaces_startResize);
				//window.removeEventListener(endEvent, blazorSpaces_endResize);

				space.Resizing = false;
				space.UpdateParent();

				var resizeEnd = OnResizeEnd ?? space.OnResizeEnd;
				if (resizeEnd != null)
				{
					//const currentRect = space.element.getBoundingClientRect();
					var currentRect = new DOMRect();
					resizeEnd(
						(int)Math.Floor(
							resizeType == ResizeType.Left || resizeType == ResizeType.Right ? 
								(decimal)currentRect.Width : 
								(decimal)currentRect.Height
						),
						currentRect
					);
				}
			}

			//window.addEventListener(moveEvent, blazorSpaces_startResize);
			//window.addEventListener(endEvent, blazorSpaces_endResize);
		}
	}
}
