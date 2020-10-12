using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System;

namespace Function
{
    public class FunctionHandler
    {
        public async Task<(int, string)> Handle(HttpRequest request)
        {
            var reader = new StreamReader(request.Body);
            var input = await reader.ReadToEndAsync();
            
            int iterations = 100;
            var msg = $"{input} is not an integer. Executing 100 iterations.";
            
            if(!int.TryParse(input, out iterations))
            {
                msg = $"Executing {input} iterations";
            }    

            await execute(iterations);

            return (200, msg);
        }

        private async Task execute(int iterations)
        {
            var t = Task.Run(async () => {
                for(var i = 0; i < iterations; i++ )
                {
                    await callFunction("cs-long", (i + 1).ToString());
                }
                Console.WriteLine($"Completed wait");
            });
        }
        private async Task callFunction(string function, string data)
        {            
            using (var client = new HttpClient())
            {
                // For kubernetes, use "gateway.openfaas:8080"
                // var uri = new Uri($"http://gateway.openfaas:8080/function/{function}");
                var uri = new Uri($"http://gateway:8080/function/{function}");
                HttpResponseMessage response = await client.PostAsync(uri, new StringContent(data, Encoding.UTF8, "text/plain"));
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Result of call to {uri.ToString()}: {result}");
            }
        }
    }
}