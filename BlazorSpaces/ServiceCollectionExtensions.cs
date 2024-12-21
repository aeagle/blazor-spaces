using Microsoft.Extensions.DependencyInjection;

namespace BlazorSpaces
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBlazorSpaces(this IServiceCollection services)
        {
            services.AddScoped<SpaceStore>();

            return services;
        }
    }
}
