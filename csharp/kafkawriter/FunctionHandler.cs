using Confluent.Kafka;
using Microsoft.AspNetCore.Http;
using Minio;
using Minio.DataModel;
using Minio.Exceptions;
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
                writeKafkaAsync(input, minio).ContinueWith(x =>
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
        
        
        private async Task<string> writeKafkaAsync(string input, MinioClient minio)
        {
            // Read task from db
            var request = getTaskFromDb(input);

            if (request.ContainsKey("error"))
            {
                return request.ToString();
            }

            var bucket = request["params"]["bucket"].Value<string>();
            var file = request["params"]["object"].Value<string>();
            var topic = request["params"]["topic"].Value<string>();


            //Get object from minio, write to string
            var data = string.Empty;
            try
            {
                // Check whether the object exists 
                await minio.StatObjectAsync(bucket, file);
                
                // Get input stream to have content of 'my-objectname' from 'my-bucketname'
                await minio.GetObjectAsync(bucket, file,
                                                    (x) =>
                                                    {
                                                        data = new StreamReader(x).ReadToEnd();
                                                    });
            }
            catch (MinioException ex)
            {
                Console.WriteLine("Error occurred: " + ex.Message);
            }

            if (string.IsNullOrEmpty(data))
            {
                return $"Object {file} not found in {bucket}";
            }

            //Write to kafka
            string result = string.Empty;

            var config = new ProducerConfig
            {
                BootstrapServers = Environment.GetEnvironmentVariable("kafka_endpoint"),
                MessageMaxBytes = 1000000000
            };

            Action<DeliveryReport<Null, string>> handler = x =>
            {
                result = !x.Error.IsError ? $"Delivered message to {x.TopicPartitionOffset}" : $"Delivery Error: {x.Error.Reason}";
                Console.WriteLine(result);
            };

            using (var producer = new ProducerBuilder<Null, string>(config).Build())
            {
                try
                {
                    var t = producer.ProduceAsync(topic, new Message<Null, string> { Value = data });
                    t.ContinueWith(x =>
                    {
                        if (x.IsFaulted)
                        {
                            result = $"Error writing to kafka: {x.Exception.Message}";
                            Console.WriteLine(result);
                        }
                        else
                        {
                            result = $"Wrote to offset: {x.Result.Offset}";
                            Console.WriteLine(result);
                        }
                    })
                    .Wait();
                }
                catch (Exception ex)
                {
                    result = $"Error writing to kafka: {ex.Message}";
                    Console.WriteLine(result);
                }
            }
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
                // New code:
                //HttpResponseMessage response = await client.GetAsync(uri);
                HttpResponseMessage response = await client.PostAsync(uri, new StringContent(input, Encoding.UTF8, "text/plain"));
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}