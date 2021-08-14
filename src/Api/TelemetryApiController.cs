using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using BufferedProducerUserTelemetry.Models;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace BufferedProducerUserTelemetry.Api
{
    [Route("api/telemetry")]
    [ApiController]
    public class TelemetryApiController : ControllerBase
    {
        private static int _counter = 0;
        private readonly EventHubBufferedProducerClient _producer;

        public TelemetryApiController(EventHubBufferedProducerClient producer) => _producer = producer;

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] TelemetryItem item)
        {
            try
            {
                // Create the event for the telemetry.

                var eventData = new EventData(JsonSerializer.Serialize(item));
                eventData.MessageId = (Interlocked.Increment(ref _counter)).ToString();
                eventData.Properties["report-time"] = DateTimeOffset.UtcNow.ToString();

                // Assign the telemetry target as the partition key to group reports
                // for that control in a single partition.

                await _producer.EnqueueEventAsync(eventData, new EnqueueEventOptions { PartitionKey = item.Element });
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
