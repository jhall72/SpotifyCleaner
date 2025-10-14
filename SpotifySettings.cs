using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyCleaner
{
    /// <summary>
    /// We get all these values from the Spotify Developer Dashboard
    /// and place them in appsettings.json or user secrets
    /// </summary>
    public class SpotifySettings
    {
        public string ClientID { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectURI { get; set; } = string.Empty;
    }
}
