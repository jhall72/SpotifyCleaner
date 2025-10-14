using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyCleaner
{
    public class TrackPosition
    {
        public string TrackId { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
        public int Position { get; set; }
    }
}
