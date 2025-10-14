using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using Swan;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace SpotifyCleaner.SpotCleaner
{
    public class SpotifyPlaylistClient : ISpotifyPlaylistClient
    {
        private readonly SpotifyClient _client;
        private readonly ILogger<SpotifyPlaylistClient> _logger;

        public SpotifyPlaylistClient(string accessToken, ILogger<SpotifyPlaylistClient>? logger = null)
        {
            _client = new SpotifyClient(accessToken);
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SpotifyPlaylistClient>.Instance;
            _logger.LogInformation("SpotifyPlaylistClient initialized");
        }

        public async Task<List<PlaylistInfo>> GetAllPlaylistsAsync(CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope("GetAllPlaylistsAsync");

            try
            {
                _logger.LogInformation("Starting to fetch all user playlists");
                var first = await _client.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 }, ct);
                var playlists = new List<PlaylistInfo>();
                _logger.LogDebug("Initial request completed, total playlists available: {Total}", first.Total);

                await foreach (var playlist in _client.Paginate(first))
                {
                    ct.ThrowIfCancellationRequested();

                    var playlistInfo = new PlaylistInfo
                    {
                        Id = playlist.Id,
                        Name = playlist.Name,
                        Owner = playlist.Owner?.DisplayName,
                        Tracks = playlist.Tracks?.Total,
                        Uri = playlist.Uri,
                        Href = playlist.Href,
                        IsPublic = playlist.Public,
                        Images = playlist.Images,
                        SnapshotId = playlist.SnapshotId
                    };
                    playlistInfo.Duplicates = await GetPlaylistDuplicateTracksAsync(playlist.Id, ct);
                    playlists.Add(playlistInfo);

                    if (playlists.Count % 10 == 0)
                    {
                        _logger.LogDebug("Processed {ProcessedCount} playlists so far", playlists.Count);
                    }
                }

                _logger.LogInformation("Successfully retrieved {Count} playlists", playlists.Count);
                return playlists;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GetAllPlaylistsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching all playlists");
                throw;
            }
        }

        public async Task<int> DeleteTrackDuplicatesAsync(string playlistId, string trackId, CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope("DeleteTrackDuplicatesAsync for playlist: {PlaylistId}", playlistId);
            try
            {
                if (string.IsNullOrEmpty(playlistId))
                {
                    _logger.LogError("PlaylistId is null or empty");
                    throw new ArgumentException("PlaylistId cannot be null or empty", nameof(playlistId));
                }
                _logger.LogInformation("Starting to delete {TrackId} specific tracks from playlist {PlaylistId}", trackId, playlistId);

                var trackPositions = await GetTrackPositionsAsync(playlistId, trackId, ct);
                if (trackPositions.Count == 0)
                {
                    _logger.LogWarning("No matching tracks found in playlist for the provided track IDs");
                    return 0;
                }

                const int batchSize = 10000; // Spotify only allows up to 100 tracks per request
                int totalRemoved = 0;

                foreach (var batch in trackPositions.Chunk(batchSize))
                {
                    ct.ThrowIfCancellationRequested();
                    var batchList = batch.ToList();

                    batchList.Sort((a, b) => b.Position.CompareTo(a.Position));

                    var removeRequest = new PlaylistRemoveItemsRequest
                    {
                        Tracks = batchList.Select(tp => new PlaylistRemoveItemsRequest.Item
                        {
                            Uri = tp.Uri,
                            Positions = new List<int> { tp.Position }
                        }).ToList()
                    };

                    await _client.Playlists.RemoveItems(playlistId, removeRequest, ct);
                    totalRemoved += batchList.Count;

                    _logger.LogDebug("Removed batch of {BatchSize} tracks, total removed: {TotalRemoved}", batchList.Count, totalRemoved);
                }

                _logger.LogInformation("Successfully removed {TotalRemoved} tracks from playlist {PlaylistId}", totalRemoved, playlistId);
                return totalRemoved;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("DeleteTrackDuplicatesAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting specific tracks from playlist {PlaylistId}", playlistId);
                throw;
            }
        }
        public async Task<bool> IsClientConnectedAsync(CancellationToken ct = default)
        {
            try
            {
                _logger.LogDebug("Testing Spotify client connection");

                var user = await _client.UserProfile.Current(ct);

                _logger.LogInformation("Client is connected. User: {DisplayName}", user.DisplayName);
                return true;
            }
            catch (APIException apiEx)
            {
                _logger.LogError(apiEx, "API Exception - Client authentication issue. Status: {Status}", apiEx.Response?.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client connection check failed");
                return false;
            }
        }
        public async Task<int> DeleteAllDuplicateTracksAsync(string playlistId, IEnumerable<string> trackIds = null, CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope("DeleteAllDuplicateTracksAsync for playlist: {PlaylistId}", playlistId);

            try
            {
                _logger.LogInformation("Checking client connection...");
                var isConnected = await IsClientConnectedAsync(ct);
                if (!isConnected)
                {
                    _logger.LogError("Client is not connected or authenticated");
                    throw new InvalidOperationException("Spotify client is not connected or token is invalid");
                }
                _logger.LogInformation("Client connection verified");

                if (string.IsNullOrEmpty(playlistId))
                {
                    _logger.LogError("PlaylistId is null or empty");
                    throw new ArgumentException("PlaylistId cannot be null or empty", nameof(playlistId));
                }

                if (trackIds == null || !trackIds.Any())
                {
                    _logger.LogInformation("No track IDs provided for removal");
                    return 0;
                }

                _logger.LogInformation("Starting to remove tracks from playlist {PlaylistId}", playlistId);

                _logger.LogInformation("Fetching all tracks from playlist...");
                var allTracks = await GetAllPlaylistTracksWithPositionsAsync(playlistId, ct);
                _logger.LogInformation("Fetched {TrackCount} total tracks from playlist", allTracks.Count);

                if (allTracks.Count == 0)
                {
                    _logger.LogInformation("No tracks found in playlist {PlaylistId}", playlistId);
                    return 0;
                }

                var trackIdSet = new HashSet<string>(trackIds, StringComparer.OrdinalIgnoreCase);
                var tracksToRemove = allTracks
                    .Where(t => trackIdSet.Contains(t.TrackId))
                    .DistinctBy(x => x.TrackId)// Keep only one instance of each track ID
                    .OrderByDescending(t => t.Position)// We want the LOWEST position first (Lowest = Highest on the playlist(),
                                                       // so we add it right back to where it should be, in my mind, everything afterwords is the duplicate
                    .ToList();

                if (tracksToRemove.Count == 0)
                {
                    _logger.LogInformation("None of the provided track IDs found in playlist {PlaylistId}", playlistId);
                    return 0;
                }

                _logger.LogInformation("Found {TrackCount} tracks to remove", tracksToRemove.Count);

                const int batchSize = 100;
                int totalRemoved = 0;
                int batchNum = 0;

                foreach (var batch in tracksToRemove.Chunk(batchSize))
                {
                    ct.ThrowIfCancellationRequested();
                    batchNum++;

                    var batchList = batch.ToList();

                    var removeRequest = new PlaylistRemoveItemsRequest
                    {
                        Tracks = batchList.Select(tp => new PlaylistRemoveItemsRequest.Item
                        {
                            Uri = tp.Uri,
                            Positions = new List<int> { tp.Position }
                        }).ToList()
                    };

                    await _client.Playlists.RemoveItems(playlistId, removeRequest, ct);
                    totalRemoved += batchList.Count;

                    _logger.LogDebug("Removed batch {BatchNum}, total removed: {TotalRemoved}",
                        batchNum, totalRemoved);
                }

                _logger.LogInformation("Successfully removed {TotalRemoved} tracks from playlist {PlaylistId}",
                    totalRemoved, playlistId);
                await AddTracksToPlaylist(playlistId, tracksToRemove.DistinctBy(x => x.TrackId).ToList(), ct);
                return totalRemoved;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("DeleteAllDuplicateTracksAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting tracks from playlist {PlaylistId}", playlistId);
                throw;
            }
        }
        private async Task AddTracksToPlaylist(string playlistId, List<TrackPosition> tracks, CancellationToken ct = default)
        {
            if (tracks == null || tracks.Count == 0)
            {
                _logger.LogDebug("No tracks to add to playlist {PlaylistId}", playlistId);
                return;
            }
            try
            {
                _logger.LogInformation("Adding {TrackCount} tracks back to playlist {PlaylistId}", tracks.Count, playlistId);

                var playlist = await _client.Playlists.Get(playlistId, ct);
                int currentPlaylistSize = playlist.Tracks?.Total ?? 0;

                var sortedTracks = tracks.OrderBy(t => t.Position).ToList();

                const int batchSize = 100;
                int totalAdded = 0;

                foreach (var batch in sortedTracks.Chunk(batchSize))
                {
                    ct.ThrowIfCancellationRequested();
                    var batchList = batch.ToList();

                    int requestedPosition = batchList[0].Position;
                    int actualPosition = Math.Min(requestedPosition, currentPlaylistSize);

                    var addRequest = new PlaylistAddItemsRequest(batchList.Select(t => t.Uri).ToList())
                    {
                        Position = actualPosition
                    };

                    await _client.Playlists.AddItems(playlistId, addRequest, ct);

                    currentPlaylistSize += batchList.Count;
                    totalAdded += batchList.Count;

                    _logger.LogDebug("Added batch of {BatchSize} tracks back to playlist at position {Position}, total added: {TotalAdded}",
                        batchList.Count, actualPosition, totalAdded);
                }

                _logger.LogInformation("Successfully added {TrackCount} tracks back to playlist {PlaylistId}",
                    sortedTracks.Count, playlistId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tracks back to playlist {PlaylistId}", playlistId);
                throw;
            }
        }
        private async Task<List<TrackPosition>> GetTrackPositionsAsync(string playlistId, string trackId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(trackId))
            {
                _logger.LogWarning("Track ID is null or empty in GetTrackPositionsAsync");
                return new List<TrackPosition>();
            }

            var trackPositions = new List<TrackPosition>();
            try
            {
                var firstPage = await _client.Playlists.GetItems(playlistId, new PlaylistGetItemsRequest(), ct);
                int position = 0;
                bool keepFirst = true;

                await foreach (var item in _client.Paginate(firstPage))
                {
                    ct.ThrowIfCancellationRequested();
                    if (item.Track is FullTrack track &&
                        !string.IsNullOrEmpty(track.Id) &&
                        string.Equals(track.Id, trackId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!keepFirst)
                        {
                            trackPositions.Add(new TrackPosition
                            {
                                TrackId = track.Id,
                                Uri = track.Uri,
                                Position = position
                            });
                            _logger.LogDebug("Found duplicate track {TrackId} at position {Position}", track.Id, position);
                        }
                        else
                        {
                            _logger.LogDebug("Keeping first occurrence of track {TrackId} at position {Position}", track.Id, position);
                            keepFirst = false;
                        }
                    }
                    position++;
                }

                _logger.LogInformation("Found {Count} duplicate occurrences of track {TrackId} in playlist {PlaylistId} (keeping first occurrence)",
                    trackPositions.Count, trackId, playlistId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding positions for track {TrackId} in playlist {PlaylistId}", trackId, playlistId);
                throw;
            }

            return trackPositions;
        }
        private async Task<List<TrackPosition>> GetAllPlaylistTracksWithPositionsAsync(string playlistId, CancellationToken ct = default)
        {
            var tracks = new List<TrackPosition>();

            try
            {
                _logger.LogDebug("Fetching first page of tracks for playlist {PlaylistId}", playlistId);

                var firstPage = await _client.Playlists.GetItems(playlistId, new PlaylistGetItemsRequest { Limit = 50 }, ct);

                _logger.LogDebug("First page retrieved, starting pagination");

                int position = 0;
                await foreach (var item in _client.Paginate(firstPage).WithCancellation(ct))
                {
                    if (item.Track is FullTrack track && !string.IsNullOrEmpty(track.Id))
                    {
                        tracks.Add(new TrackPosition
                        {
                            TrackId = track.Id,
                            Uri = track.Uri,
                            Position = position
                        });
                    }
                    position++;
                }

                _logger.LogDebug("Pagination complete, found {TrackCount} tracks", tracks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllPlaylistTracksWithPositionsAsync for playlist {PlaylistId}", playlistId);
                throw;
            }

            return tracks;
        }

        public async Task<Dictionary<FullTrack, int>> GetPlaylistDuplicateTracksAsync(string playlistId, CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope("GetPlaylistDuplicateTracksAsync for playlist: {PlaylistId}", playlistId);

            try
            {
                var trackCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var trackDetails = new Dictionary<string, FullTrack>(StringComparer.OrdinalIgnoreCase);

                var firstPage = await _client.Playlists.GetItems(playlistId, new PlaylistGetItemsRequest { Limit = 50 }, ct);

                await foreach (var item in _client.Paginate(firstPage))
                {
                    ct.ThrowIfCancellationRequested();

                    if (item.Track is FullTrack track && !string.IsNullOrEmpty(track.Id))
                    {
                        trackCounts[track.Id] = trackCounts.TryGetValue(track.Id, out var count) ? count + 1 : 1;

                        if (!trackDetails.ContainsKey(track.Id))
                        {
                            trackDetails[track.Id] = track;
                        }
                    }
                }

                var duplicateTracksWithCounts = trackCounts
                    .Where(kvp => kvp.Value > 1)
                    .ToDictionary(
                        kvp => trackDetails[kvp.Key],
                        kvp => kvp.Value - 1 // Subtract 1 to get the number of duplicates beyond the first occurrence (we keep the first one)
                    );

                _logger.LogInformation("Found {DuplicateTrackCount} duplicate tracks in playlist {PlaylistId}", duplicateTracksWithCounts.Count, playlistId);
                return duplicateTracksWithCounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting duplicate tracks for playlist {PlaylistId}", playlistId);
                throw;
            }
        }
    }
}