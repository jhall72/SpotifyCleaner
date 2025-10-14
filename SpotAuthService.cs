using Microsoft.Extensions.Options;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Threading.Tasks;

namespace SpotifyCleaner.SpotAuthService
{
    public class SpotAuthService
    {
        private readonly string _ClientId;
        private readonly string _ClientSecret;
        private readonly Uri _RedirectURI;

        public SpotAuthService(SpotifySettings spotifySettings)
        {
            _ClientId = spotifySettings.ClientID;
            _ClientSecret = spotifySettings.ClientSecret;
            _RedirectURI = new Uri(spotifySettings.RedirectURI);
        }

        public Uri GetLoginUri(string? state = null)
        {
            var loginRequest = new LoginRequest(
                _RedirectURI,
                _ClientId,
                LoginRequest.ResponseType.Code
            )
            {
                Scope = new[]
                {
                    Scopes.PlaylistReadPrivate,
                    Scopes.PlaylistReadCollaborative,
                    Scopes.PlaylistModifyPrivate,
                    Scopes.PlaylistModifyPublic
                },
                State = state
            };

            return loginRequest.ToUri();
        }

        public async Task<AuthorizationCodeTokenResponse> ExchangeCodeForTokenAsync(string code)
        {
            var oAuth = new OAuthClient();
            var response = await oAuth.RequestToken(
                new AuthorizationCodeTokenRequest(
                    _ClientId,
                    _ClientSecret,
                    code,
                    _RedirectURI
                )
            );

            return response;
        }

        public SpotifyClient CreateClient(string accessToken)
        {
            return new SpotifyClient(accessToken);
        }
    }
}