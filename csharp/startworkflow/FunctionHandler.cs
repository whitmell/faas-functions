using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Function
{
    public class FunctionHandler
    {
        ELAPSWorkflowHandler elaps = new ELAPSWorkflowHandler(Environment.GetEnvironmentVariable("mongoEndpoint"));
        
        public async Task<(int, string)> Handle(HttpRequest request)
        {
            elaps.FunctionName = "startworkflow";
            #region Function Setup

            // Start timer
            elaps.StartTimer();
            await elaps.LogStartAsync();

            //Read input string
            var reader = new StreamReader(request.Body);
            var input = await reader.ReadToEndAsync();
            
            #endregion

            await elaps.ReadWorkflowDoc(input);
            await elaps.CallFirstFunction();

            #region Function Teardown

            //Stop timer
            var duration = elaps.StopTimer();
            await elaps.LogStopAsync();

            #endregion
            
            return (200, $"Workflow {input} was started.");
        }
    }
}