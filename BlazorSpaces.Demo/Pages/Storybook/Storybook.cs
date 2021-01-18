using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace BlazorBook
{
    public class StoryComponent : ComponentBase
    {
    }

    public static class Components
    {
        static Dictionary<string, Type> _components = new();

        public static IEnumerable<string> ComponentNames => _components.Keys;

        public static Type TypeByName(string component) => _components[component];

        public static void Register(Type t)
        {
            _components.Add(t.FullName.Replace(".", "_").ToLower(), t);
        }
    }
}