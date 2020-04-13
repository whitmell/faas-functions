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
    public class ELAPSWorkflowHandler
    {
        Stopwatch timer;
        MongoClient mongo;
        public ELAPSWorkflow Workflow { get; set; }

        public string FunctionName { get; set; }
        public ELAPSWorkflowHandler()
        {
            Workflow = new ELAPSWorkflow { Functions = new List<ELAPSFunction>() };
        }

        public ELAPSWorkflowHandler(string mongoEndpoint) : this()
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
            Console.WriteLine($"{FunctionName} finished in {timer.ElapsedMilliseconds} ms.");
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

        public async Task ReadWorkflowDoc(string name)
        {
            if (mongo == null)
                return;

            try
            {
                var database = mongo.GetDatabase("elaps");
                var collection = database.GetCollection<BsonDocument>("workflows");
                var filter = Builders<BsonDocument>.Filter.Eq("name", name);
                var task = collection.Find(filter).FirstOrDefault();
                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };

                var result = JObject.Parse(task.ToJson<BsonDocument>(jsonWriterSettings));
                parseWorkflowDoc(result);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error retrieving function doc: {ex.Message}");
            }
        }

        public async Task CallFirstFunction()
        {
            
            //Write function call doc
            await writeFunctionCallDocAsync(Workflow.Functions[0]);
            await callFunction(Workflow.Functions[0]);
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

        private void parseWorkflowDoc(JObject doc)
        {
            if (doc.Type == JTokenType.Null)
                return;

            try
            {
                Workflow.Name = doc["name"].Value<string>();
                Workflow.Active = doc["active"].Value<bool>();
                Workflow.Priority = doc["priority"].Value<int>();
                Workflow.Functions = doc["functions"].Children().Select(x => x.ToObject<ELAPSFunction>()).ToList();
            }
            catch(Exception ex)
            {
                throw new Exception($"Error parsing workflow doc: {ex.Message}");
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
