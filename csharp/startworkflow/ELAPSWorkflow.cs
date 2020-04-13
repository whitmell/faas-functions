using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Function
{
    public class ELAPSWorkflow
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("active")]
        public bool Active { get; set; }
        [JsonProperty("priority")]
        public int Priority { get; set; }
        [JsonProperty("functions")]
        public List<ELAPSFunction> Functions { get; set; }

        public JObject ToJObject()
        {
            return JObject.FromObject(this);
            //return new JObject(
            //    new JProperty("name", Name),
            //    new JProperty("params", JObject.FromObject(Parameters)),
            //    new JProperty("functions", new JArray(Children.Select(x => x.ToJObject())))
            //    );

        }
    }
}