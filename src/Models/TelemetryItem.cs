using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BufferedProducerUserTelemetry.Models
{
    public class TelemetryItem
    {
        public string Element { get; set; }
        public string Action { get; set; }
        public string Value { get; set; }
    }
}
