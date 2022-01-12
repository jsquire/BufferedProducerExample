using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Logging;

namespace BufferedProducerUserTelemetry
{
    /// <summary>
    ///   This class abstracts all interactions with an <see cref="EventHubBufferedProducerClient" />
    ///   for publishing telemetry.
    /// </summary>
    ///
    public class TelemetryPublisher
    {
        /// <summary>The logger instance to use for emitting messages.</summary>
        private ILogger _logger;

        /// <summary>
        ///   The <see cref="EventHubBufferedProducerClient" /> to be used for publishing
        ///   telemetry.
        /// </summary>
        ///
        public EventHubBufferedProducerClient Producer { get; }

        /// <summary>
        ///   Creates a new <see cref="TelemetryPublisher" /> instance.
        /// </summary>
        ///
        /// <param name="eventHubsConnectionString">The connection string to be used by the producer.</param>
        /// <param name="eventHubName">The name of the Event Hub to which telemetry should be published.</param>
        /// <param name="options">The options that should govern the producer.</param>
        ///
        public TelemetryPublisher(string eventHubsConnectionString,
                                  string eventHubName,
                                  EventHubBufferedProducerClientOptions options)
        {
            var producer = new EventHubBufferedProducerClient(eventHubsConnectionString, eventHubName, options);
            producer.SendEventBatchSuccessAsync += EventBatchSendSuccessHandlerAsync;
            producer.SendEventBatchFailedAsync += EventBatchSendFailedHandlerAsync;

            Producer = producer;
        }

        /// <summary>
        ///   Allows specifying a logger that can be used for emitting
        ///   messages for telemetry publishing operations.
        /// </summary>
        ///
        /// <param name="logger">The logger instance to set.</param>
        ///
        public void SetLogger(ILogger logger) => _logger = logger;

        /// <summary>
        ///   The handler responsible for taking action when a batch of telemetry events
        ///   has successfully been published.
        /// </summary>
        ///
        /// <param name="args">The <see cref="SendEventBatchSuccessEventArgs"/> instance associated with the notification.</param>
        ///
        private Task EventBatchSendSuccessHandlerAsync(SendEventBatchSuccessEventArgs args)
        {
            var ids = string.Join(',', args.EventBatch.Select(evt => evt.MessageId));
            _logger?.LogInformation($"A batch was successfully published.  Messages contained: [{ ids }]");

            return Task.CompletedTask;
        }

        /// <summary>
        ///   The handler responsible for taking action when a batch of telemetry events
        ///   has failed to be been published successfully.
        /// </summary>
        ///
        /// <param name="args">The <see cref="SendEventBatchSuccessEventArgs"/> instance associated with the notification.</param>
        ///
        private async Task EventBatchSendFailedHandlerAsync(SendEventBatchFailedEventArgs args)
        {
            var exception = args.Exception;
            var wasEnqueued = false;

            _logger?.LogWarning($"A batch of { args.EventBatch.Count() } events failed to publish with the exception: `{ exception.Message }`.  It will be retried if possible and dead-lettered if not.");

            while ((!wasEnqueued) && (ShouldRetry(exception)))
            {
                try
                {
                    await Producer.EnqueueEventsAsync(args.EventBatch);
                    wasEnqueued = true;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    _logger?.LogWarning($"A batch of { args.EventBatch.Count() } events failed to be enqueued on retry with the exception: `{ exception.Message }`.  It will be retried if possible and dead-lettered if not.");
                }
            }

            // If the batch could not be enqueued, then dead-letter the events
            // and the current exception for later remediation.

            if (!wasEnqueued)
            {
                // The target method has responsibility for managing retries, failures,
                // and logging for the dead-letter operation.

                await DeadletterToStorageSafeAsync(args.EventBatch, exception);
            }
        }

        /// <summary>
        ///   Captures a set of events that could not be published and preserves them
        ///   in Azure Storage for later inspection and remediation.
        ///
        ///   If the events could not be written to storage, they will be individually logged.
        /// </summary>
        ///
        /// <param name="events">The events to dead-letter.</param>
        /// <param name="exception">The exception to associate with the dead-lettered events.</param>
        ///
        /// <remarks>
        ///   This method will not throw; it holds responsibility for interacting with storage
        ///   in a safe manner, falling back to logging the events in the case that storage was
        ///   unavailable.
        /// </remarks>
        ///
        private async Task DeadletterToStorageSafeAsync(IEnumerable<EventData> events,
                                                        Exception exception)
        {
            // TODO: Add this functionality; for now, this is orthogonal to the
            // concept being demonstrated and the details are unimportant.

            _logger?.LogWarning($"A batch of { events.Count() } events has been dead-lettered with the exception: `{ exception.Message }`.");
            await Task.Delay(500);
        }

        /// <summary>
        ///   Determines if an exception should be considered eligible for retrying.
        /// </summary>
        ///
        /// <param name="exception">The exception to consider.</param>
        ///
        /// <returns><c>true</c> if the <paramref name="exception" /> is eligible to retry; otherwise, <c>false</c>.</returns>
        ///
        private bool ShouldRetry(Exception exception) =>
            exception switch
            {
                EventHubsException ehEx => ehEx.IsTransient,
                TimeoutException _ => true,
                _ => false
            };
    }
}
