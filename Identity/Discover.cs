using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;

namespace GingerMintSoft.WorkFlows.Identity
{
    public class Discover
    {
        private readonly HttpClient _client = new HttpClient();

        public string Token { get; set; }
        public DiscoveryDocumentResponse Disco { get; set; }

        public async Task<bool> CheckEndpoint(string uri)
        {
            // discover endpoints from metadata
            Disco = await _client.GetDiscoveryDocumentAsync(uri);

            return !Disco.IsError;
        }

        public async Task<string> RequestToken()
        {
            // request token
            var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = Disco.TokenEndpoint,
                ClientId = "ro.angular",
                ClientSecret = "secret",
                Scope = "api1"
            });

            if (tokenResponse.IsError)
                return null;

            Token = tokenResponse.AccessToken;
            return tokenResponse.AccessToken;
        }
    }
}
