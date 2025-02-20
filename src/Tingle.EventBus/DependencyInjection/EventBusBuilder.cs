﻿using Microsoft.Extensions.DependencyInjection.Extensions;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Ids;
using Tingle.EventBus.Internal;
using Tingle.EventBus.Serialization;
using Tingle.EventBus.Transports;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A builder class for adding and configuring the EventBus in <see cref="IServiceCollection"/>.
/// </summary>
public class EventBusBuilder
{
    /// <summary>
    /// Creates an instance of <see cref="EventBusBuilder"/>
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configureHostService"></param>
    public EventBusBuilder(IServiceCollection services, Action<IServiceCollection>? configureHostService = null)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));

        // Configure the options
        Services.ConfigureOptions<EventBusConfigureOptions>();

        // Register the bus and its host
        Services.TryAddSingleton<EventBus>();

        configureHostService ??= services => services.AddHostedService<EventBusHost>();
        configureHostService.Invoke(Services);

        // Register necessary services
        Services.AddSingleton<IEventConfigurator, DefaultEventConfigurator>(); // can be multiple do not use TryAdd*(...)
        Services.TryAddSingleton<EventBusTransportProvider>();
        Services.TryAddSingleton<IEventBusConfigurationProvider, DefaultEventBusConfigurationProvider>();
        Services.TryAddSingleton<IEventIdGenerator, DefaultEventIdGenerator>();
        Services.TryAddTransient<IEventPublisher, EventPublisher>();
        UseDefaultSerializer<DefaultJsonEventSerializer>();
    }

    /// <summary>
    /// The instance of <see cref="IServiceCollection"/> that this builder instance adds to.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>Configure options for the EventBus.</summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder Configure(Action<EventBusOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        Services.Configure(configure);
        return this;
    }

    /// <summary>Configure serialization options for the EventBus.</summary>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder ConfigureSerialization(Action<EventBusSerializationOptions> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        Services.Configure(configure);
        return this;
    }

    /// <summary>Adds a <see cref="EventBusTransportRegistration"/> which can be used by the event bus.</summary>
    /// <typeparam name="THandler">The <see cref="EventBusTransport{TOptions}"/> used to handle this transport.</typeparam>
    /// <typeparam name="TOptions">The <see cref="EventBusTransportOptions"/> type to configure the transport."/>.</typeparam>
    /// <typeparam name="TConfigurator">The <see cref="EventBusTransportConfigureOptions{TOptions}"/> type to configure the <typeparamref name="TOptions"/>."/>.</typeparam>
    /// <param name="name">The name of this transport.</param>
    /// <param name="configureOptions">Used to configure the transport options.</param>
    public EventBusBuilder AddTransport<THandler, TOptions, TConfigurator>(string name, Action<TOptions>? configureOptions)
        where THandler : EventBusTransport<TOptions>
        where TOptions : EventBusTransportOptions, new()
        where TConfigurator : EventBusTransportConfigureOptions<TOptions>
        => AddTransport<THandler, TOptions, TConfigurator>(name, displayName: null, configureOptions: configureOptions);

    /// <summary>Adds a <see cref="EventBusTransportRegistration"/> which can be used by the event bus.</summary>
    /// <typeparam name="THandler">The <see cref="EventBusTransport{TOptions}"/> used to handle this transport.</typeparam>
    /// <typeparam name="TOptions">The <see cref="EventBusTransportOptions"/> type to configure the transport."/>.</typeparam>
    /// <typeparam name="TConfigurator">The <see cref="EventBusTransportConfigureOptions{TOptions}"/> type to configure the <typeparamref name="TOptions"/>."/>.</typeparam>
    /// <param name="name">The name of this transport.</param>
    /// <param name="displayName">The display name of this transport.</param>
    /// <param name="configureOptions">Used to configure the transport options.</param>
    public EventBusBuilder AddTransport<THandler, TOptions, TConfigurator>(string name, string? displayName, Action<TOptions>? configureOptions)
        where THandler : EventBusTransport<TOptions>
        where TOptions : EventBusTransportOptions, new()
        where TConfigurator : EventBusTransportConfigureOptions<TOptions>
        => AddTransportHelper<THandler, TOptions, TConfigurator>(name, displayName, configureOptions);

    private EventBusBuilder AddTransportHelper<TTransport, TOptions, TConfigurator>(string name, string? displayName, Action<TOptions>? configureOptions)
        where TTransport : class, IEventBusTransport
        where TOptions : EventBusTransportOptions, new()
        where TConfigurator : EventBusTransportConfigureOptions<TOptions>
    {
        Services.Configure<EventBusOptions>(o => o.AddTransport<TTransport>(name, displayName));

        // Calling ConfigureOptions results in more than one call to the configuration implementations so we have to implement our own logic
        // TODO: remove after https://github.com/dotnet/runtime/issues/42358 is addressed

        static IEnumerable<Type> FindConfigurationServices(Type type)
        {
            var interfaces = type.GetInterfaces();
            foreach (var t in interfaces)
            {
                if (t.IsGenericType)
                {
                    var gtd = t.GetGenericTypeDefinition();
                    if (gtd == typeof(Options.IConfigureOptions<>) || gtd == typeof(Options.IPostConfigureOptions<>) || gtd == typeof(Options.IValidateOptions<>))
                    {
                        yield return t;
                    }
                }
            }
        }
        foreach (var serviceType in FindConfigurationServices(typeof(TConfigurator)))
        {
            Services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, typeof(TConfigurator)));
        }

        if (configureOptions is not null) Services.Configure(name, configureOptions);

        // //  transport is not registered because multiple separate instances are required per name
        // Services.TryAddSingleton<TTransport>();
        return this;
    }

    /// <summary>
    /// Setup the default serializer to use when serializing events to and from the EventBus transport.
    /// </summary>
    /// <typeparam name="TEventSerializer"></typeparam>
    /// <returns></returns>
    public EventBusBuilder UseDefaultSerializer<TEventSerializer>() where TEventSerializer : class, IEventSerializer
    {
        Services.AddSingleton<IEventSerializer, TEventSerializer>();
        return this;
    }

    /// <summary>
    /// Subscribe to events that a consumer can listen to.
    /// </summary>
    /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder AddConsumer<TConsumer>(Action<EventRegistration, EventConsumerRegistration> configure) where TConsumer : class, IEventConsumer
    {
        var consumerType = typeof(TConsumer);
        if (consumerType.IsAbstract)
        {
            throw new InvalidOperationException($"Abstract consumer types are not allowed.");
        }

        var eventTypes = new List<(Type type, bool deadletter)>();

        // get events from each implementation of IEventConsumer<TEvent> or IDeadLetteredEventConsumer<TEvent>
        var interfaces = consumerType.GetInterfaces();
        foreach (var type in interfaces)
        {
            if (!type.IsGenericType) continue;

            var gtd = type.GetGenericTypeDefinition();
            if (gtd != typeof(IEventConsumer<>) && gtd != typeof(IDeadLetteredEventConsumer<>)) continue;

            eventTypes.Add((type.GenericTypeArguments[0], gtd == typeof(IDeadLetteredEventConsumer<>)));
        }

        // we must have at least one implemented event
        if (eventTypes.Count <= 0)
        {
            throw new InvalidOperationException($"{consumerType.FullName} must implement '{nameof(IEventConsumer)}<TEvent>' at least once.");
        }

        foreach (var (et, _) in eventTypes)
        {
            if (et.IsAbstract)
            {
                throw new InvalidOperationException($"Invalid event type '{et.FullName}'. Abstract types are not allowed.");
            }
        }

        // add the event types to the registrations
        return Configure(options =>
        {
            foreach (var (et, deadletter) in eventTypes)
            {
                // get or create a simple EventRegistration
                var reg = options.Registrations.GetOrAdd(et, t => new EventRegistration(t));

                // create a simple ConsumerRegistration (HashSet removes duplicates)
                var ecr = new EventConsumerRegistration(consumerType, deadletter);
                reg.Consumers.Add(ecr);

                // call the configuration function
                configure?.Invoke(reg, ecr);
            }
        });
    }

    /// <summary>
    /// Subscribe to events that a consumer can listen to.
    /// </summary>
    /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EventBusBuilder AddConsumer<TConsumer>(Action<EventConsumerRegistration>? configure = null) where TConsumer : class, IEventConsumer
    {
        return AddConsumer<TConsumer>((reg, ecr) => configure?.Invoke(ecr));
    }

    /// <summary>
    /// Unsubscribe to events that a consumer can listen to.
    /// </summary>
    /// <typeparam name="TConsumer"></typeparam>
    /// <returns></returns>
    public EventBusBuilder RemoveConsumer<TConsumer>() where TConsumer : class, IEventConsumer
    {
        // Remove the event types
        return Configure(options =>
        {
            var ct = typeof(TConsumer);
            foreach (var registration in options.Registrations.Values)
            {
                registration.Consumers.RemoveWhere(creg => creg.ConsumerType == ct);
            }
        });
    }
}
