﻿using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Options for configuring Azure Service Bus based event bus.
    /// </summary>
    public class AzureServiceBusOptions
    {
        /// <summary>
        /// The connection string to Azure Service Bus.
        /// When not configured, <see cref="ConnectionStringProperties"/> must be provided.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The connection options to Azure Service Bus.
        /// When not set, <see cref="ConnectionString"/> is used to create it.
        /// </summary>
        public ServiceBusConnectionStringProperties ConnectionStringProperties { get; set; }

        /// <summary>
        /// The type of transport to use.
        /// Defaults to <see cref="ServiceBusTransportType.AmqpTcp"/>
        /// </summary>
        public ServiceBusTransportType TransportType { get; set; } = ServiceBusTransportType.AmqpTcp;

        /// <summary>
        /// A setup function for setting up options for a topic.
        /// This is only called before creation.
        /// </summary>
        public Action<CreateTopicOptions> SetupTopicOptions { get; set; }

        /// <summary>
        /// A setup function for setting up options for a subscription.
        /// This is only called before creation.
        /// </summary>
        public Action<CreateSubscriptionOptions> SetupSubscriptionOptions { get; set; }
    }
}
