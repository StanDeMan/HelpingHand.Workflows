using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.Client;

namespace GingerMintSoft.WorkFlows.Payment
{
    // internal: GingerMintSoft.WorkFlows.
    using Identity;

    public class Http
    {
        private Http()
        {
            _client = new HttpClient();
        }

        public static async Task<Http> Create(string baseUri)
        {
            var http = new Http {BaseUri = baseUri};
            await http.RequestToken();
            return http;
        }

        private string _token;
        private readonly HttpClient _client;

        public string BaseUri { get; set; }
        public bool IsError { get; set; }

        public string Token
        {
            get => _token;
            set
            {
                _token = value;
                _client.SetBearerToken(_token);
            }
        }

        private async Task RequestToken()
        {
            try
            {
                var discover = new Discover();

                if (!await discover.CheckEndpoint(BaseUri)) return;

                Token = await discover.RequestToken();
            }
            catch (Exception )
            {
                Token = null;
                IsError = true;
            }
        }

        public async Task<HttpResponseMessage> PostAsync(string relativeUri, string json)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(5000);

            try
            {
                var uri = new Uri($"{BaseUri}/{relativeUri}");
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(uri, httpContent, cts.Token);

                return response.IsSuccessStatusCode 
                    ? response 
                    : null;
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                IsError = true;
                return null;
            }
        }

        public async Task<HttpResponseMessage> GetAsync(string relativeUri)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(5000);

            try
            {
                var uri = new Uri($"{BaseUri}/{relativeUri}");
                var response = await _client.GetAsync(uri, cts.Token);

                return response.IsSuccessStatusCode 
                    ? response 
                    : null;
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                IsError = true;
                return null;
            }
        }
    }
}