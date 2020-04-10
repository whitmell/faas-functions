using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Function
{
    public class FunctionHandler
    {
        public string Handle(string input) {
           
            string result = string.Empty;
            makeCallAsync(input).ContinueWith(x =>
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
                    result = x.Result;
                }
            })
            .Wait();
            return result;
        }

        private async Task<string> makeCallAsync(string input)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://gateway:8080/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

                // New code:
                HttpResponseMessage response = await client.PostAsync("function/figlet", new StringContent(input));
                return await response.Content.ReadAsStringAsync();
            }

        }
    }
}
