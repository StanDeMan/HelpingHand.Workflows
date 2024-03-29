﻿using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.Client;

namespace GingerMintSoft.WorkFlows.Communication
{
    // internal: GingerMintSoft.WorkFlows.Communication.
    using Identity;

    public class Http
    {
        private const int TimeOut = 10000;

        private Http()
        {
            _client = new HttpClient();
        }

        public static async Task<Http> Create(string baseUri = null)
        {
            Http http;

            if (baseUri == null)
            {
#if DEBUG
                const string baseUriOwn = "http://localhost:52719";
#else
                const string baseUriOwn = "https://www.werebuzy.com";
#endif
                http = new Http
                {
                    BaseUri = baseUriOwn
                };
            }
            else
            {            
                http = new Http
                {
                    BaseUri = baseUri
                };
            }

            await http.RequestToken();
            return http;
        }

        private string _token;
        private readonly HttpClient _client;

        public string BaseUri { get; set; }
        public bool IsError { get; set; }

        public bool ResetError
        {
            set => IsError = !value;
        }

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
            cts.CancelAfter(TimeOut);

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

        public async Task<HttpResponseMessage> PutAsync(string relativeUri, string json)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeOut);

            try
            {
                var uri = new Uri($"{BaseUri}/{relativeUri}");
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PutAsync(uri, httpContent, cts.Token);

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
            cts.CancelAfter(TimeOut);

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