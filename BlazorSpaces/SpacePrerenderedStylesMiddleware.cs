using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;

namespace BlazorSpaces
{
    public class BlazorSpacesStylePrerenderMiddleware
    {
        private readonly RequestDelegate _next;

        public BlazorSpacesStylePrerenderMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, SpaceStore store)
        {
            if (!store.HasPrerenderedStyles)
            {
                await _next(context);
            }
            else
            {
                var originalBodyStream = context.Response.Body;

                using (var newBodyStream = new MemoryStream())
                {
                    context.Response.Body = newBodyStream;

                    await _next(context);

                    newBodyStream.Seek(0, SeekOrigin.Begin);
                    var originalHtml = await new StreamReader(newBodyStream).ReadToEndAsync();
                    var manipulatedHtml = originalHtml.Replace("</head>", $"{store.RenderPrerenderedStyles()}</head>");

                    newBodyStream.Seek(0, SeekOrigin.Begin);
                    await using (var writer = new StreamWriter(originalBodyStream))
                    {
                        await writer.WriteAsync(manipulatedHtml);
                    }
                }
            }
        }
    }

}
