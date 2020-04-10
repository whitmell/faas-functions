using Microsoft.AspNetCore.Http;
using Minio;
using Minio.DataModel;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Function
{
    public class FunctionHandler
    {
        public async Task<(int, string)> Handle(HttpRequest request)
        {
            var reader = new StreamReader(request.Body);
            var input = await reader.ReadToEndAsync();

            var parms = input.Split('|');
            input = parms[0];
            var newBucket = (parms.Length > 1) ? parms[1] : $"{input}-copy";
            Console.WriteLine($"NEWBUCKET: {newBucket}");

            var minio = new MinioClient(Environment.GetEnvironmentVariable("minio_endpoint"),
                                Environment.GetEnvironmentVariable("minio_access_key"),
                                Environment.GetEnvironmentVariable("minio_secret_key")
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
                        var key = Guid.NewGuid().ToString();
                        var callJson = new JObject(
                        new JProperty("_key", key),
                            new JProperty("params", new JObject(
                                new JProperty("bucket", input),
                                new JProperty("object", f),
                                new JProperty("newBucket", newBucket))
                            ));
                        writeTaskToDb(callJson);
                        string callResponse = string.Empty;
                        makeCallAsync(key).ContinueWith(x =>
                        {
                            if(x.IsFaulted)
                                callResponse = $"Error calling objectmover for {f}: {x.Exception.Message}";
                            else
                                callResponse = x.Result;
                            Console.WriteLine($"{input}: {callResponse}");
                        }).Wait();
                        json.Add(new JProperty($"call-{f}", callResponse));
                    }
                });
                observable.Wait();
            return (200, json.ToString());
        }

        private void writeTaskToDb(JObject json)
        {
            var client = new MongoClient(string.Format("mongodb://{0}", Environment.GetEnvironmentVariable("mongo_endpoint")));
            var database = client.GetDatabase(Environment.GetEnvironmentVariable("mongo_database"));

            var document = BsonDocument.Parse(json.ToString());
            var collection = database.GetCollection<BsonDocument>(Environment.GetEnvironmentVariable("mongo_collection"));
            collection.InsertOne(document);
        }

        private async Task<string> makeCallAsync(string input)
        {
            using ( var client = new HttpClient())
            {
                //var uri = new Uri($"http://gateway:8080/api/objectmover/{input}");
                var uri = new Uri($"http://gateway:8080/function/objectmover");
                // New code:
                //HttpResponseMessage response = await client.GetAsync(uri);
                HttpResponseMessage response = await client.PostAsync(uri, new StringContent(input, Encoding.UTF8, "text/plain"));
                return await response.Content.ReadAsStringAsync();
            }

        }
    }
}
