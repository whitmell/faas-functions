using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Function
{
    public class ELAPSFunctionHandler
    {
        Stopwatch timer;
        MongoClient mongo;
        public ELAPSFunction Function { get; set; }

        public ELAPSFunctionHandler()
        {
            Function = new ELAPSFunction { Parameters = new Dictionary<string, string>(), Children = new List<ELAPSFunction>() };
        }

        public ELAPSFunctionHandler(string mongoEndpoint) : this()
        {
            mongo = new MongoClient($"mongodb://{mongoEndpoint}");
        }

        public void StartTimer()
        {
            timer = Stopwatch.StartNew();
        }

        public TimeSpan StopTimer()
        {
            timer.Stop();
            return timer.Elapsed;
        }

        public async Task LogStartAsync()
        {
            //LOG start to db or send message
        }
        public async Task LogStopAsync()
        {
            //LOG stop to db or send message
        }

        public async Task ReadFunctionCallDoc(string key)
        {
            if (mongo == null)
                return;

            try
            {
                var database = mongo.GetDatabase("elaps");
                var collection = database.GetCollection<BsonDocument>("functioncalls");
                var filter = Builders<BsonDocument>.Filter.Eq("_key", key);
                var task = collection.Find(filter).FirstOrDefault();
                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };

                var result = JObject.Parse(task.ToJson<BsonDocument>(jsonWriterSettings));
                parseFunctionDoc(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving function doc: {ex.Message}");
            }
        }

        public async Task CallChildren()
        {
            if (Function.Children.Any())
            {
                // Set up each child function
                foreach (var child in Function.Children)
                {
                    //Write inherited parameters down to child
                    foreach (var p in Function.Parameters)
                    {
                        if (child.Parameters.ContainsKey(p.Key))
                        {
                            child.Parameters[p.Key] = p.Value;
                        }
                        else
                        {
                            child.Parameters.Add(p.Key, p.Value);
                        }
                    }

                    //Write function call doc
                    await writeFunctionCallDocAsync(child);
                    await callFunction(child);
                }
            }
        }

        private async Task writeFunctionCallDocAsync(ELAPSFunction child)
        {
            if (mongo == null)
                return;

            var database = mongo.GetDatabase("elaps");
            child.Key = Guid.NewGuid().ToString();
            var document = BsonDocument.Parse(child.ToJson().ToString());
            var collection = database.GetCollection<BsonDocument>("functioncalls");
            await collection.InsertOneAsync(document);
            //LOG function call created
        }

        private void parseFunctionDoc(JObject doc)
        {
            if (doc.Type == JTokenType.Null)
                return;

            try
            {
                Function.Name = doc["name"].Value<string>();
                Function.Name = doc["name"].Value<string>();
                Function.Parameters = doc["params"].ToObject<Dictionary<string, string>>();
                Function.Children = doc["functions"].Children().Select(x => x.ToObject<ELAPSFunction>()).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing function doc: {ex.Message}");
            }
        }

        private async Task<string> callFunction(ELAPSFunction function)
        {
            using (var client = new HttpClient())
            {
                var uri = new Uri($"http://gateway:8080/api/{function}");
                HttpResponseMessage response = await client.PostAsync(uri, new StringContent(function.Key, Encoding.UTF8, "text/plain"));
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}
