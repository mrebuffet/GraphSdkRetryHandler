using System;

namespace GraphSdkRetryHandler
{
    static class Program
    {
        static void Main(string[] args)
        {
            bool useCustomRetryHandler = true;
            GraphService graphService = new GraphService(useCustomRetryHandler);
            try
            {
                string myEmail = graphService.GetMyDetailsWithHttpClient().ConfigureAwait(false).GetAwaiter().GetResult();
                Console.WriteLine($"Http Client: {myEmail}");

                myEmail = graphService.GetMyDetailsWithGraphClient().ConfigureAwait(false).GetAwaiter().GetResult();
                Console.WriteLine($"Graph Client: {myEmail}");

                (string httpResult, string graphResult) = graphService.GetUser().ConfigureAwait(false).GetAwaiter().GetResult();
                Console.WriteLine($"Joined call: http = {httpResult} / graph = {graphResult}");

                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
