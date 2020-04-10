using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Function
{
    public class FunctionHandler
    {
        public string Handle(string country) {
            return extract(country);
        }

        private string extract(string input)
        {
            try
            {
                string json = (new WebClient()).DownloadString("https://covidtracking.com/api/states");
                
                var covidObj = JArray.Parse(json);
                foreach (var c in covidObj)
                {
                    var state = c["state"].Value<string>();
                    if (string.Compare(state.Trim(), input.Trim(), StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"COVID Stats for {state}");
                        sb.AppendLine("-------------------------------------");
                        sb.AppendLine("Confirmed: " + (c["positive"].Type == JTokenType.Null ? 0 : c["positive"].Value<int>()));
                        sb.AppendLine("Deaths: " + (c["death"].Type == JTokenType.Null ? 0 : c["death"].Value<int>()));
                        sb.AppendLine("LastUpdated: " + (c["lastUpdateEt"] == null || c["lastUpdateEt"].Type == JTokenType.Null ? string.Empty : c["lastUpdateEt"].Value<string>()));

                        return sb.ToString();
                    }
                }

                return $"No COVID data found for state {input}";
            }
            catch (Exception e)
            {
                return $"Error extracting data: {e.Message}";
            }

        }
    }
}
