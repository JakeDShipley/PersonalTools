using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PersonalTools.Data.Monitoring;

namespace PersonalTools.Logging;

public static class ApplicationLogProviderExtensions
{
    public static ILoggingBuilder AddApplicationLogViewer(this ILoggingBuilder builder)
    {
        builder.Services.TryAddSingleton<IApplicationLogStore, ApplicationLogStore>();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, ApplicationLogProvider>());

        return builder;
    }
}