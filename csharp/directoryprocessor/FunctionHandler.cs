using Minio;
using Minio.DataModel;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Function
{
    public class FunctionHandler
    {
        public string Handle(string input)
        {
            string result = "Nothing happened?";
            try
            {
                var minio = new MinioClient("localhost:9000",
                                "minioadmin",
                                "minioadmin"
                                );
                processDirectory(input, minio).ContinueWith((x) =>
                {
                    if(x.IsFaulted)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine(x.Exception.Message);
                        foreach(var e in x.Exception.InnerExceptions)
                        {
                            sb.AppendLine(e.Message);
                        }
                        
                        result = sb.ToString();
                    }
                    else
                    {
                        result = x.Result;
                    }
                    
                }).Wait();
            }
            catch(Exception ex)
            {
                result = $"Error calling object storage: {ex.Message}";
            }
            return result;
        }

        private async Task<string> processDirectory(string input, MinioClient minio)
        {
            
            var filesJson = new JArray();
            var result = string.Empty;
            var message = string.Empty;

            
            IObservable<Item> observable = minio.ListObjectsAsync(input);

            IDisposable subscription = observable.Subscribe(
                item => 
                {
                    filesJson.Add(new JObject(new JProperty("fileName", item.Key)));

                    var json = new JObject(
                            new JProperty("bucket", "ar-data"),
                            new JProperty("file", $"{item.Key}"),
                            new JProperty("newBucket", "ar-copy-test-2"));
                },
                e =>
                {
                    var json = new JObject(
                        new JProperty("result", "fail"),
                        new JProperty("message", e.Message));
                    Console.Write(json.ToString());
                },
                () =>
                {
                    var json = new JObject(
                        new JProperty("result", "success"),
                        new JProperty("message", $"Successfully processed directory {input}"),
                        new JProperty("files", filesJson));
                    Console.Write(json.ToString());
                });

            return $"Listing bucket {input}.";
        }
    }
}
