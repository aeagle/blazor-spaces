using Microsoft.AspNetCore.Components;

namespace BlazorSpaces
{
    public class Resources : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "script");
            builder.AddContent(1, (MarkupString)CoreUtils.GetEmbeddedWebResource("spaces.js"));
            builder.CloseElement();
            builder.OpenElement(0, "style");
            builder.AddContent(1, (MarkupString)CoreUtils.GetEmbeddedWebResource("spaces.css"));
            builder.CloseElement();
        }
    }
}
