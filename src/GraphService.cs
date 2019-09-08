using Microsoft.Graph;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace GraphSdkRetryHandler
{
    public class GraphService
    {
        private const string GraphBaseAddress = "https://graph.microsoft.com/v1.0/";
        private readonly string graphToken = Properties.Resources.GraphToken;

        private GraphServiceClient GraphClient { get; set; }
        private DelegateAuthenticationProvider AuthenticationProvider { get; }

        public GraphService(bool useCustomRetryHandler = false)
        {
            if (string.IsNullOrWhiteSpace(this.graphToken))
            {
                throw new InvalidOperationException("The Graph Token is missing");
            }

            this.AuthenticationProvider = new DelegateAuthenticationProvider((requestMessage) =>
            {
                requestMessage
                .Headers
                .Authorization = new AuthenticationHeaderValue("Bearer", this.graphToken);

                return Task.FromResult(0);
            });

            this.GraphClient = useCustomRetryHandler ? this.CreateGraphClientWithCustomRetryHandler() : this.CreateGraphClientWithDefaultRetryHandler();
            this.GraphClient.HttpProvider.OverallTimeout = new TimeSpan(0, 0, 30);
        }

        private GraphServiceClient CreateGraphClientWithDefaultRetryHandler()
        {
            return new GraphServiceClient(this.AuthenticationProvider);
        }

        private GraphServiceClient CreateGraphClientWithCustomRetryHandler()
        {
            IList<DelegatingHandler> handlers = new List<DelegatingHandler> { new AuthenticationHandler(this.AuthenticationProvider) }; // GraphClientFactory.CreateDefaultHandlers(this.AuthenticationProvider);

            // Remove buggy integrated RetryHandler and use our Custom RetryHandler instead
            DelegatingHandler defaultRetryHandler = handlers.FirstOrDefault(handler => handler is RetryHandler);
            if (defaultRetryHandler != null)
            {
                handlers.Remove(defaultRetryHandler);
            }

            // Add a modified retry handler to the HttpClient pipeline which will prevent the Graph SDK to retry failed requests
            Func<int, int, HttpResponseMessage, bool> shouldRetry = new Func<int, int, HttpResponseMessage, bool>(this.ShouldRetry);
            handlers.Add(new RetryHandler(new RetryHandlerOption() { Delay = 3, MaxRetry = 3, ShouldRetry = shouldRetry }));

            // Add an error logging handler to the httpClient pipeline
            // handlers.Add(new GraphSdkLoggingHandler());
            HttpProvider customHttpProvider = new HttpProvider(GraphClientFactory.CreatePipeline(handlers), false, new Serializer());
            return new GraphServiceClient(this.AuthenticationProvider, customHttpProvider);
        }

        /// <summary>
        /// Delegate function indicating whether we should retry the request given the delay, the attempt number and the <see cref="HttpResponseMessage" />.
        /// </summary>
        /// <param name="delay">The delay.</param>
        /// <param name="attempt">The attempt number.</param>
        /// <param name="response">The <see cref="HttpResponseMessage" />.</param>
        /// <returns>A boolean indicating whether the request should be retried or not.</returns>
        private bool ShouldRetry(int delay, int attempt, HttpResponseMessage response)
        {
            return false;
        }

        public async Task<string> GetMyDetailsWithHttpClient()
        {
            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = new Uri(GraphBaseAddress);
                    httpClient.Timeout = new TimeSpan(0, 0, 30);
                    HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "me");
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.graphToken);
                    HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseFromServer = await response.Content.ReadAsStringAsync();
                        User me = JsonConvert.DeserializeObject<User>(responseFromServer);
                        return me.Mail;
                    }
                    else
                    {
                        string error = string.Empty;
                        if (response.Content != null)
                        {
                            error = await response.Content.ReadAsStringAsync();
                        }

                        return $"Invalid request - Error {(int)response.StatusCode} ({response.StatusCode}) / {response.ReasonPhrase} - {error}";
                    }
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> GetMyDetailsWithGraphClient()
        {
            try
            {
                User me = await this.GraphClient.Me.Request().Select("Mail").WithMaxRetry(3).GetAsync();
                return me.Mail;
            }
            catch (ServiceException ex)
            {
                return ex.Message;
            }
        }

        public async Task<(string httpResult, string graphResult)> GetUser()
        {
            Task<string> graphCall = this.GetMyDetailsWithGraphClient();
            Task<string> httpCall = this.GetMyDetailsWithHttpClient();

            await Task.WhenAll(graphCall, httpCall);

            if (httpCall.Result != null && httpCall.Result.Equals(graphCall.Result))
            {
                return (graphCall.Result, httpCall.Result);
            }
            else
            {
                return (string.Empty, string.Empty);
            }
        }
    }
}
