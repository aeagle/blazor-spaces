using Microsoft.AspNetCore.Components;

namespace BlazorSpaces
{
    public class PreRender : ComponentBase
    {
        [Inject] public SpaceStore store { get; set; }

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.AddContent(0, (MarkupString)store.RenderDeferredStyles());
        }
    }
}
