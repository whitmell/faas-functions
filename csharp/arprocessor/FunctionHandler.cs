using Minio;
using Newtonsoft.Json.Linq;
using System;
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
                processFile(input, minio).ContinueWith((x) =>
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

        private async Task<string> processFile(string input, MinioClient minio)
        {
            var request = JObject.Parse(input);
            var bucket = request["bucket"].Value<string>();
            var file = request["file"].Value<string>();
            var newBucket = request["newBucket"].Value<string>();

            bool found = await minio.BucketExistsAsync(newBucket);
            if(!found)
            {
                await minio.MakeBucketAsync(newBucket);
            }
            string result = string.Empty;
            minio.CopyObjectAsync(bucket, file, newBucket).ContinueWith((x) =>
            {
                if(x.IsFaulted)
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
                    result = "File copied successfully";
                }
            })
            .Wait();
            return result;
        }
    }
}
