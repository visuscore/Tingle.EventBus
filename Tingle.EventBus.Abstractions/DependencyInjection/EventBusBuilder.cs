﻿using System;
using System.Collections.Generic;
using System.Linq;
using Tingle.EventBus.Abstractions;
using Tingle.EventBus.Abstractions.Serialization;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A builder class for adding and configuring the EventBus in <see cref="IServiceCollection"/>.
    /// </summary>
    public class EventBusBuilder
    {
        /// <summary>
        /// Creates an instance os <see cref="EventBusBuilder"/>
        /// </summary>
        /// <param name="services"></param>
        public EventBusBuilder(IServiceCollection services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// The instance of <see cref="IServiceCollection"/> that this builder instance adds to.
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Configure options for EventBus
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public EventBusBuilder Configure(Action<EventBusOptions> configure)
        {
            Services.Configure(configure);
            return this;
        }

        /// <summary>
        /// Setup the serializer to use when serializing events to and from the EventBus transport.
        /// </summary>
        /// <typeparam name="TEventSerializer"></typeparam>
        /// <returns></returns>
        public EventBusBuilder UseSerializer<TEventSerializer>() where TEventSerializer : class, IEventSerializer
        {
            Services.AddSingleton<IEventSerializer, TEventSerializer>();
            return this;
        }

        /// <summary>
        /// Use serializer powered by <see href="https://www.nuget.org/packages/Newtonsoft.Json/">Newtonsoft.Json</see>.
        /// </summary>
        /// <returns></returns>
        public EventBusBuilder UseNewtonsoftJsonSerializer()
        {
            return UseSerializer<NewtonsoftJsonEventSerializer>();
        }

        /// <summary>
        /// Subscribe to events that a consumer can listen to.
        /// </summary>
        /// <typeparam name="TConsumer">The type of consumer to handle the events.</typeparam>
        /// <returns></returns>
        public EventBusBuilder Subscribe<TConsumer>() where TConsumer : class, IEventBusConsumer
        {
            // register resolution for this type
            Services.AddTransient<TConsumer>();

            var genericConsumerType = typeof(IEventBusConsumer<>);
            var eventTypes = new List<Type>();

            // get events from each implementation of IEventConsumer<TEvent>
            var consumerType = typeof(TConsumer);
            var interfaces = consumerType.GetInterfaces();
            foreach (var ifType in interfaces)
            {
                if (ifType.IsGenericType && ifType.GetGenericTypeDefinition() == genericConsumerType)
                {
                    var et = ifType.GenericTypeArguments[0];
                    eventTypes.Add(et);
                }
            }

            // we must have at least one implemented event
            if (eventTypes.Count <= 0)
            {
                throw new InvalidOperationException($"{consumerType.FullName} must implement '{nameof(IEventBusConsumer)}<TEvent>' at least once.");
            }

            // add the event types to the registrations
            return Configure(options =>
            {
                foreach (var et in eventTypes)
                {
                    // if the type is already mapped to another consumer, throw meaningful exception
                    if (options.EventRegistrations.TryGetValue(et, out var registration)
                        && registration.ConsumerType != consumerType)
                    {
                        throw new InvalidOperationException($"{et.FullName} cannot be mapped to {consumerType.FullName} as it is already mapped to {registration.ConsumerType.FullName}");
                    }

                    options.EventRegistrations[et] = new EventConsumerRegistration(et, consumerType);
                }
            });
        }

        /// <summary>
        /// Unsubscribe to events that a consumer can listen to.
        /// </summary>
        /// <typeparam name="TConsumer"></typeparam>
        /// <returns></returns>
        public EventBusBuilder Unsubscribe<TConsumer>() where TConsumer : class, IEventBusConsumer
        {
            return Configure(options =>
            {
                var registrations = options.EventRegistrations.Where(kvp => kvp.Value.ConsumerType == typeof(TConsumer))
                                                              .Select(kvp => kvp.Value)
                                                              .ToList();
                foreach (var r in registrations)
                {
                    options.EventRegistrations.Remove(r.EventType);
                }
            });
        }
    }
}
