using Minio;
using Minio.DataModel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Function
{
    public class FunctionHandler
    {
        public Task<string> Handle(string input)
        {
            var minio = new MinioClient("gateway:9000",
                                "minioadmin",
                                "minioadmin"
                                );

            var objectsJson = new JArray();
            var objects = new List<string>();
            var result = string.Empty;
            var message = string.Empty;
            JObject json = new JObject(new JProperty("result", "success"));

            IObservable<Item> observable = minio.ListObjectsAsync(input);

            IDisposable subscription = observable.Subscribe(
                item =>
                {
                    objects.Add(item.Key);
                    objectsJson.Add(new JObject(new JProperty("name", item.Key)));
                },
                e =>
                {
                    json = new JObject();
                    json.Add(new JProperty("result", "fail"));
                    json.Add(new JProperty("exception", e.Message));
                },
                () =>
                {
                    json.Add(new JProperty("objects", objectsJson));
                    foreach (var f in objects)
                    {
                        var callJson = new JObject();
                        callJson.Add(new JProperty("bucket", input));
                        callJson.Add(new JProperty("object", f));
                        callJson.Add(new JProperty("newBucket", "ar-data-test-openfaas"));
                        makeCallAsync(callJson.ToString()).Wait();
                    }
                });
            return json.ToString();
        }

        private async Task<string> makeCallAsync(string input)
        {
            using ( var client = new HttpClient())
            {
                var uri = new Uri("https://gateway:8080/api/objectmover/");
                // New code:
                HttpResponseMessage response = await client.PostAsJsonAsync<JObject>(uri, JObject.Parse(input));
                return await response.Content.ReadAsStringAsync();
            }

        }

    }
}
