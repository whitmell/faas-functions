using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Function
{
    public class ELAPSFunction
    {
        [JsonProperty("_key")]
        public string Key { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("workflow")]
        public string Workflow { get; set; }
        [JsonProperty("params")]
        public Dictionary<string, string> Parameters { get; set; }
        [JsonProperty("functions")]
        public List<ELAPSFunction> Children { get; set; }

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