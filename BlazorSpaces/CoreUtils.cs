using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BlazorSpaces
{
    public static class CoreUtils
    {
        public static string GetEmbeddedWebResource(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"BlazorSpaces.wwwroot.{name}"))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static ValueTask UpdateStyleDefinition(IJSRuntime JS, string spaceId, string definition)
        {
            return JS.InvokeVoidAsync("spaces_updateStyleDefinition", spaceId, definition);
        }

        public static ValueTask RemoveStyleDefinition(IJSRuntime JS, string spaceId)
        {
            return JS.InvokeVoidAsync("spaces_removeStyleDefinition", spaceId);
        }

        public static async Task<DOMRect> GetBoundingClientRect(IJSRuntime JS, ElementReference element)
        {
            return await JS.InvokeAsync<DOMRect>("spaces_getBoundingClientRect", element);
        }

        private static ConcurrentDictionary<(string, string), Func<EventArgs, Task>> RegisteredEvents = new();

        public static async ValueTask RegisterEvent<T>(IJSRuntime JS, string spaceId, string eventName, Func<EventArgs, Task> action)
            where T : EventArgs
        {
            if (!RegisteredEvents.ContainsKey((spaceId, eventName)))
            {
                RegisteredEvents.TryAdd((spaceId, eventName), action);
                await JS.InvokeVoidAsync("spaces_registerevent", spaceId, eventName);
            }
        }

        public static ValueTask UnregisterEvent(IJSRuntime JS, string spaceId, string eventName)
        {
            RegisteredEvents.TryRemove((spaceId, eventName), out var _);
            return JS.InvokeVoidAsync("spaces_unregisterevent", spaceId, eventName);
        }

        [JSInvokable("spaces_notifyevent")]
        public static async Task NotifyEvent(string spaceId, string eventName, EventInteropArgs args)
        {
            if (RegisteredEvents.TryGetValue((spaceId, eventName), out var eventHandler))
            {
                switch (eventName)
                {
                    case "mousemove":
                    case "mouseup":
                        await eventHandler(
                            new MouseEventArgs { 
                                ClientX = args.ClientX, 
                                ClientY = args.ClientY 
                            }
                        );
                        break;
                    case "touchmove":
                    case "touchend":
                        await eventHandler(
                            new TouchEventArgs { 
                                Touches = 
                                    args.Touches.Select(t => 
                                        new TouchPoint { 
                                            ClientX = t.ClientX, 
                                            ClientY = t.ClientY 
                                        }).ToArray() 
                            }
                        );
                        break;
                }
            }
        }

        public class EventInteropArgs : EventInteropClientCoords
        { 
            public IEnumerable<EventInteropClientCoords> Touches { get; set; }
        }

        public class EventInteropClientCoords
        {
            public double ClientX { get; set; }
            public double ClientY { get; set; }
        }

    }
}
