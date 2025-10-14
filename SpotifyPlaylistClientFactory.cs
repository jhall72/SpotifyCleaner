using Microsoft.Extensions.Logging;
using SpotifyCleaner.SpotCleaner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyCleaner
{
    public class SpotifyPlaylistClientFactory : ISpotifyPlaylistClientFactory
    {
        private readonly ILogger<SpotifyPlaylistClient> _logger;

        public SpotifyPlaylistClientFactory(ILogger<SpotifyPlaylistClient> logger)
        {
            _logger = logger;
        }

        public ISpotifyPlaylistClient CreateClient(string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new ArgumentException("Access token cannot be null or empty. Please use the SpotifyAPI to get your application's Access Token", nameof(accessToken));
            }

            return new SpotifyPlaylistClient(accessToken, _logger);
        }
    }
}
