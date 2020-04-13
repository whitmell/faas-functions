using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Function
{
    public class FunctionHandler
    {
        ELAPSFunctionHandler elaps = new ELAPSFunctionHandler(Environment.GetEnvironmentVariable("mongoEndpoint"));
        public async Task<(int, string)> Handle(HttpRequest request)
        {
            #region Function Setup

            // Start timer
            elaps.StartTimer();
            await elaps.LogStartAsync();

            //Read input string
            var reader = new StreamReader(request.Body);
            var input = await reader.ReadToEndAsync();

            await elaps.ReadFunctionCallDoc(input);

            #endregion

            await Execute();

            await elaps.CallChildren();

            #region Function Teardown

            //Stop timer
            var duration = elaps.StopTimer();
            await elaps.LogStopAsync();

            #endregion

            return (200, $"Hello! Your input was {input}");
        }

        public async Task<string> Execute()
        {
            return "Function executed successfully";
        }
    }
}