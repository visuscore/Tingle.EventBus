using Tingle.EventBus.Internal;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods on <see cref="IServiceCollection"/> for EventBus.
/// </summary>
public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// Add Event Bus services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to add services to.</param>
    /// <param name="configureHostService">
    /// An optional action for setting up the host service. If <see langword="null"/> the default <see cref="EventBusHost"/> will be used.
    /// </param>
    /// <returns>An <see cref="EventBusBuilder"/> to continue setting up the Event Bus.</returns>
    public static EventBusBuilder AddEventBus(this IServiceCollection services, Action<IServiceCollection>? configureHostService = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        return new EventBusBuilder(services, configureHostService);
    }

    /// <summary>
    /// Add Event Bus services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance to add services to.</param>
    /// <param name="setupAction">An optional action for setting up the bus.</param>
    /// <param name="configureHostService">
    /// An optional action for setting up the host service. If <see langword="null"/> the default <see cref="EventBusHost"/> will be used.
    /// </param>
    /// <returns></returns>
    public static IServiceCollection AddEventBus(
        this IServiceCollection services,
        Action<EventBusBuilder>? setupAction = null,
        Action<IServiceCollection>? configureHostService = null)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        var builder = services.AddEventBus(configureHostService);

        setupAction?.Invoke(builder);

        return services;
    }
}
