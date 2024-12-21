using Microsoft.AspNetCore.Components;

namespace BlazorSpaces
{
    public class Resources : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "script");
            builder.AddAttribute(1, "src", "_content/BlazorSpaces/spaces.js");
            builder.CloseElement();
            builder.OpenElement(0, "link");
            builder.AddAttribute(1, "href", "_content/BlazorSpaces/spaces.min.css");
            builder.AddAttribute(2, "rel", "stylesheet");
            builder.CloseElement();
        }
    }
}
