using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyCleaner
{
    public class PlaylistInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Owner { get; set; }
        public int? Tracks { get; set; }
        public string? Uri { get; set; }
        public string? Href { get; set; }
        public bool? IsPublic { get; set; }
        public IList<Image>? Images { get; set; }
        public string? SnapshotId { get; set; }
        public Dictionary<FullTrack, int> Duplicates { get; set; } = new Dictionary<FullTrack, int>();
    }
}
