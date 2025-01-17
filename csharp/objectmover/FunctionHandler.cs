using Microsoft.AspNetCore.Http;
using Minio;
using Minio.DataModel;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Function
{
    public class FunctionHandler
    {
        public async Task<(int, string)> Handle(HttpRequest request)
        {
            var reader = new StreamReader(request.Body);
            var input = await reader.ReadToEndAsync();
            string result = "Nothing happened?";
            try
            {
                var minio = new MinioClient(Environment.GetEnvironmentVariable("minio_endpoint"),
                                Environment.GetEnvironmentVariable("minio_access_key"),
                                Environment.GetEnvironmentVariable("minio_secret_key")
                                );
                moveObject(input, minio).ContinueWith(x =>
                {
                    if (x.IsFaulted)
                        result = x.Exception.Message;
                    else
                        result = x.Result;
                }).Wait();
            }
            catch (Exception ex)
            {
                result = $"Error calling object storage: {ex.Message}";
            }
            
            return (200, result);
        }

        private JObject getTaskFromDb(string key)
        {
            JObject result = new JObject();
            try 
            {
                var client = new MongoClient(string.Format("mongodb://{0}", Environment.GetEnvironmentVariable("mongo_endpoint")));
                var database = client.GetDatabase(Environment.GetEnvironmentVariable("mongo_database"));
                var collection = database.GetCollection<BsonDocument>(Environment.GetEnvironmentVariable("mongo_collection"));
                var filter = Builders<BsonDocument>.Filter.Eq("_key", key);
                var task = collection.Find(filter).FirstOrDefault();
                var jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.Strict };
            
                result = JObject.Parse(task.ToJson<BsonDocument>(jsonWriterSettings));
            }
            catch(Exception ex)
            {
                result.Add(new JProperty("error", ex.Message));
            }
            return result;
        }
        
        private async Task<string> moveObject(string input, MinioClient minio)
        {
            var request = getTaskFromDb(input);

            if(request.ContainsKey("error"))
            {
                return request.ToString();
            }

            var bucket = request["params"]["bucket"].Value<string>();
            var file = request["params"]["object"].Value<string>();
            var newBucket = request["params"]["newBucket"].Value<string>();

            bool found = await minio.BucketExistsAsync(newBucket);
            if (!found)
            {
                await minio.MakeBucketAsync(newBucket);
            }
            string result = string.Empty;
            minio.CopyObjectAsync(bucket, file, newBucket).ContinueWith((x) =>
            {
                if (x.IsFaulted)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(x.Exception.Message);
                    foreach (var e in x.Exception.InnerExceptions)
                    {
                        sb.AppendLine(e.Message);
                    }

                    result = sb.ToString();
                }
                else
                {
                    Console.WriteLine($"Object {file} copied to {newBucket} successfully");

                    var key = Guid.NewGuid().ToString();
                    var callJson = new JObject(
                    new JProperty("_key", key),
                        new JProperty("params", new JObject(
                            new JProperty("bucket", bucket),
                            new JProperty("object", file),
                            new JProperty("topic", "augury"))
                        ));
                    writeTaskToDb(callJson);
                    string callResponse = string.Empty;
                    makeCallAsync("kafkawriter", key).ContinueWith(c =>
                    {
                        if(x.IsFaulted)
                            callResponse = $"Error calling objectmover for {file}: {c.Exception.Message}";
                        else
                            callResponse = c.Result;
                        Console.WriteLine($"{input}: {callResponse}");
                    }).Wait();
                    result = callResponse;
                }
            })
            .Wait();
            return result;
        }


        private void writeTaskToDb(JObject json)
        {
            var client = new MongoClient(string.Format("mongodb://{0}", Environment.GetEnvironmentVariable("mongo_endpoint")));
            var database = client.GetDatabase(Environment.GetEnvironmentVariable("mongo_database"));

            var document = BsonDocument.Parse(json.ToString());
            var collection = database.GetCollection<BsonDocument>(Environment.GetEnvironmentVariable("mongo_collection"));
            collection.InsertOne(document);
        }

        private async Task<string> makeCallAsync(string function, string input)
        {
            using ( var client = new HttpClient())
            {
                var uri = new Uri($"http://gateway:8080/function/{function}");
                HttpResponseMessage response = await client.PostAsync(uri, new StringContent(input, Encoding.UTF8, "text/plain"));
                return await response.Content.ReadAsStringAsync();
            }

        }
    }
}