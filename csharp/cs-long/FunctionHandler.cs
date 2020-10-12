using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Function
{
    public class FunctionHandler
    {
        public async Task<(int, string)> Handle(HttpRequest request)
        {
            var reader = new StreamReader(request.Body);
            var input = await reader.ReadToEndAsync();

            await execute(input);
            return (200, $"Executing iteration {input}");
        }

        private async Task execute(string iteration)
        {
            var t = Task.Run(async () => {
                await Task.Delay(30000);
                Console.WriteLine($"Completed iteration {iteration}");
            });
        }
    }
}