using SpotifyAPI.Web;
using SpotifyCleaner.SpotCleaner;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyCleaner
{
    public interface ISpotifyPlaylistClient
    {
        /// <summary>
        /// Gets all playlists for the authenticated user
        /// Displays them in a list with their names and IDs
        /// and how many duplicates each have
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of all user playlists</returns>
        Task<List<PlaylistInfo>> GetAllPlaylistsAsync(CancellationToken ct = default);

        /// <summary>
        /// Deletes specific duplicate tracks from a playlist based on track IDs
        /// </summary>
        /// <param name="playlistId">The ID of the playlist to modify</param>
        /// <param name="trackIds">List of track IDs to remove</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Number of tracks successfully removed</returns>
        Task<int> DeleteTrackDuplicatesAsync(string playlistId, string trackId, CancellationToken ct = default);

        /// <summary>
        /// Deletes all duplicate tracks in a playlist, keeping only one instance of each unique track
        /// </summary>
        /// <param name="playlistId">The ID of the playlist to clean</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Number of duplicate tracks removed</returns>
        Task<int> DeleteAllDuplicateTracksAsync(string playlistId, IEnumerable<string> trackIds, CancellationToken ct = default);
    }
}
