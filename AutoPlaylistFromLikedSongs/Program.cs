using AutoPlaylistFromLikedSongs;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;
using SpotifyAPI.Web.Auth;
using static SpotifyAPI.Web.Scopes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using System.Collections;
using Swan;

namespace AutoPlaylistFromLikeSongs;

public class Program
{
    private class PlayListAddItemCache
    {
        public string PlaylistId { get; set; }
        public List<string> TrackUri { get; set; }
    }


    private const string CredentialsPath = "credentials.json";
    private static EmbedIOAuthServer? _server;
    private static Spotify _settings;
    private static IList<SimplePlaylist>? _playlists;
    private const string playlistPrefix = "Liked Songs - ";
    private static void Exiting() => Console.CursorVisible = true;

    public static async Task Main()
    {
        IConfiguration appConfig = new ConfigurationBuilder()
                                                .AddJsonFile("appsettings.json")
                                                .Build();
        _settings = appConfig.GetRequiredSection("Spotify").Get<Spotify>();
        if (_settings.PlaylistDiscoverAlbumOfLikedSongs && _settings.PlaylistDiscoverAlbumOfLikedSongsMinimumAlbumYear <= 0)
        {
            throw new InvalidOperationException("Invalid PlaylistDiscoverAlbumOfLikedSongsMinimumAlbumYear value");
        }

        // This is a bug in the SWAN Logging library, need this hack to bring back the cursor
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => Exiting();
        await StartAuthentication();
        Console.ReadKey();
        _server.Dispose();
    }
    private static async Task CreatePlayListFromLikeSongs(string accessToken)
    {
        var config = SpotifyClientConfig
              .CreateDefault(accessToken);
              //.WithHTTPLogger(new SimpleConsoleHTTPLogger());

        var spotify = new SpotifyClient(config);

        var me = await spotify.UserProfile.Current();
        Console.WriteLine($"Welcome {me.DisplayName} ({me.Id}), you're authenticated!");

        _playlists = await spotify.PaginateAll(await spotify.Playlists.CurrentUsers().ConfigureAwait(false));

        var page = await spotify.Library.GetTracks(new LibraryTracksRequest() { Limit = 50, Offset = 0 });

        int counter = 0;

        //empting managed playlists
        foreach (var playlist in _playlists.Where(pl => pl.Name.StartsWith(playlistPrefix)))
        {
            if (playlist.Tracks.Total > 0)
            {
                Console.WriteLine($"Clearing playlist - {playlist.Name}");
                await spotify.Playlists.ReplaceItems(playlist.Id, new PlaylistReplaceItemsRequest(new List<string> { }));
            }
            /*if(playlist.Tracks.Total == 0) //uncomment to "delete" a playlist
            {
                await spotify.Follow.UnfollowPlaylist(playlist.Id);
            }*/

        }

        var playListAddItemCaches = new Dictionary<string, List<string>>();

        var artistCaches = new Dictionary<string, FullArtist>();

        var genrePlaylistsKeywords = new List<string>();
        genrePlaylistsKeywords.Add("Metal");
        genrePlaylistsKeywords.Add("Rock");
        genrePlaylistsKeywords.Add("Pop");
        genrePlaylistsKeywords.Add("Punk");
        genrePlaylistsKeywords.Add("Quebecois");

        var likedAlbumUri = new List<string>();
        var likedTrackUri = new List<string>();

        await foreach (var likedSong in spotify.Paginate(page))
        {
            Console.WriteLine($"Processing {counter} - Songs {likedSong.Track.Name}");

            likedTrackUri.Add(likedSong.Track.Uri);

            var likedPlaylistNamesToAddSongTo = new List<string>();

            likedPlaylistNamesToAddSongTo.Add($"{playlistPrefix}{likedSong.Track.Album.ReleaseDate.Substring(0, 3)}0s");
            likedPlaylistNamesToAddSongTo.Add($"{playlistPrefix}{me.DisplayName}");
            if (_settings.PlaylistByYear)
            {
                likedPlaylistNamesToAddSongTo.Add($"{playlistPrefix}{likedSong.Track.Album.ReleaseDate.Substring(0, 4)}");
            }

            if (likedSong.AddedAt >= DateTime.Now.Subtract(TimeSpan.FromDays(30)))
            {
                likedPlaylistNamesToAddSongTo.Add($"{playlistPrefix} Liked in last 30 days");
            }

            if (likedSong.AddedAt >= DateTime.Now.Subtract(TimeSpan.FromDays(90)))
            {
                likedPlaylistNamesToAddSongTo.Add($"{playlistPrefix} Liked in last 90 days");
            }

            if (_settings.PlaylistByGenre)
            {
                if (!artistCaches.ContainsKey(likedSong.Track.Artists.First().Id))
                {
                    Console.WriteLine($"Fetching genre for artist {likedSong.Track.Artists.First().Name}");
                    var artist = await spotify.Artists.Get(likedSong.Track.Artists.First().Id);
                    artistCaches.Add(artist.Id, artist);
                }

                foreach (var genre in artistCaches[likedSong.Track.Artists.First().Id].Genres)
                {
                    foreach (var genrePlaylistsKeyword in genrePlaylistsKeywords)
                    {
                        if (genre.ToUpper().Contains(genrePlaylistsKeyword.ToUpper()))
                        {
                            string genrePlaylistName = $"{playlistPrefix} {genrePlaylistsKeyword}";
                            if (!likedPlaylistNamesToAddSongTo.Contains(genrePlaylistName))
                            {
                                likedPlaylistNamesToAddSongTo.Add(genrePlaylistName);
                            }
                        }
                    }
                }
            }

            if(_settings.PlaylistDiscoverAlbumOfLikedSongs)
            {
                if (int.Parse(likedSong.Track.Album.ReleaseDate.Substring(0, 4)) >= _settings.PlaylistDiscoverAlbumOfLikedSongsMinimumAlbumYear)
                {
                    likedAlbumUri.AddWhen(likedAlbumUri.IndexOf(likedSong.Track.Album.Id) == -1, likedSong.Track.Album.Id);
                }
            }

            /*if (likedSong.AddedAt >= DateTime.Now.Subtract(TimeSpan.FromDays(365)))
            {
                likedPlaylistNamesToAddSongTo.Add($"{playlistPrefix} Liked in last 12 months");
            }*/

            foreach (var likedPlaylistName in likedPlaylistNamesToAddSongTo)
            {
                if (_playlists.Where(pl => pl.Name == (likedPlaylistName)).Count() == 0)
                {
                    Console.WriteLine($"Creating new playlist {likedPlaylistName}");
                    await spotify.Playlists.Create(me.Id, new PlaylistCreateRequest(likedPlaylistName)
                    {
                        Public = false,
                    });
                    _playlists = await spotify.PaginateAll(await spotify.Playlists.CurrentUsers().ConfigureAwait(false));
                }
                var playlist = _playlists.Where(pl => pl.Name == (likedPlaylistName)).First();

                if (!playListAddItemCaches.ContainsKey(playlist.Id))
                {
                    playListAddItemCaches.Add(playlist.Id, new List<string> { likedSong.Track.Uri });
                }
                else
                {
                    playListAddItemCaches[playlist.Id].Add(likedSong.Track.Uri);
                }
                if (playListAddItemCaches[playlist.Id].Count >= 99)
                {
                    Console.WriteLine($"Bulk adding - Playlist {likedPlaylistName}");
                    await spotify.Playlists.AddItems(playlist.Id, new PlaylistAddItemsRequest(playListAddItemCaches[playlist.Id]));
                    playListAddItemCaches[playlist.Id].Clear();
                }
            }
            counter++;
        }

        foreach (var playListAddItemCache in playListAddItemCaches)
        {
            Console.WriteLine($"Last pass Bulk adding - Playlist Id {playListAddItemCache.Key}");
            await spotify.Playlists.AddItems(playListAddItemCache.Key, new PlaylistAddItemsRequest(playListAddItemCache.Value));
        }

        foreach (var playlist in _playlists.Where(pl => pl.Name.StartsWith(playlistPrefix)))
        {
            Console.WriteLine($"Updating playlist description and setting it public {playlist.Name}");
            spotify.Playlists.ChangeDetails(playlist.Id, new PlaylistChangeDetailsRequest() { Public = true, Description = $"Updated on {DateTime.Now.ToString()}" });
        }

        if (_settings.PlaylistDiscoverAlbumOfLikedSongs)
        {
            await CreatePlaylistAlbumOfLikedSongs(spotify, likedAlbumUri, likedTrackUri);
        }

        Console.WriteLine("Done!");
    }

    private static async Task CreatePlaylistAlbumOfLikedSongs(SpotifyClient spotify, List<string> likedAlbumUri, List<string>  likedTrackUri)
    {
        var me = await spotify.UserProfile.Current();
        int counter = 0;
        foreach (var albumId in likedAlbumUri)
        {
            counter++;
            var album = await spotify.Albums.Get(albumId);
            Console.WriteLine($"Processing album {album.Name} {counter}/{likedAlbumUri.Count}");

            string playlistToAddTo = $"{playlistPrefix} Discover Rest of Album - {album.ReleaseDate.Substring(0, 4)}";

            if (_playlists.Where(pl => pl.Name == (playlistToAddTo)).Count() == 0)
            {
                Console.WriteLine($"Creating new playlist {playlistToAddTo}");
                await spotify.Playlists.Create(me.Id, new PlaylistCreateRequest(playlistToAddTo)
                {
                    Public = false,
                });
                _playlists = await spotify.PaginateAll(await spotify.Playlists.CurrentUsers().ConfigureAwait(false));
            }
            var playlist = _playlists.Where(pl => pl.Name == (playlistToAddTo)).First();
            // Initialize a list to store the track URIs for the playlist
            var trackUris = new List<string>();

            // Iterate through the tracks in the album
            foreach (var track in album.Tracks.Items)
            {
                // If the track has not been liked, add it to the trackUris list
                if (!likedTrackUri.Contains(track.Uri))
                {
                    trackUris.Add(track.Uri);
                }
            }
            if (trackUris.Count > 0)
            {
                Console.WriteLine($"Bulk adding {trackUris.Count} songs from album {album.Name} to Playlist {playlistToAddTo}");
                // Add the tracks to the playlist
                await spotify.Playlists.AddItems(playlist.Id, new PlaylistAddItemsRequest(trackUris));
            }
        }
    }

    private static async void AddSongToPlaylist(string userId, string trackUri, string playlistname,Dictionary<string, List<string>> playListAddItemCaches, SpotifyClient spotify)
    {
        if (_playlists.Where(pl => pl.Name == (playlistname)).Count() == 0)
        {
            Console.WriteLine($"Creating new playlist {playlistname}");
            await spotify.Playlists.Create(userId, new PlaylistCreateRequest(playlistname)
            {
                Public = false,
            });
            _playlists = await spotify.PaginateAll(await spotify.Playlists.CurrentUsers().ConfigureAwait(false));
        }
        var playlist = _playlists.Where(pl => pl.Name == (playlistname)).First();

        if (!playListAddItemCaches.ContainsKey(playlist.Id))
        {
            playListAddItemCaches.Add(playlist.Id, new List<string> { trackUri });
        }
        else
        {
            playListAddItemCaches[playlist.Id].Add(trackUri);
        }
        if (playListAddItemCaches[playlist.Id].Count >= 99)
        {
            Console.WriteLine($"Bulk adding - Playlist {playlistname}");
            await spotify.Playlists.AddItems(playlist.Id, new PlaylistAddItemsRequest(playListAddItemCaches[playlist.Id]));
            playListAddItemCaches[playlist.Id].Clear();
        }
    }

    private static async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
    {
        await _server!.Stop();

        var config = SpotifyClientConfig.CreateDefault();
        var tokenResponse = await new OAuthClient(config).RequestToken(
          new AuthorizationCodeTokenRequest(
            _settings.ClientId, _settings.ClientSecret, response.Code, new Uri("http://localhost:5000/callback")
          )
        );

        await CreatePlayListFromLikeSongs(tokenResponse.AccessToken);
    }

    private static async Task OnErrorReceived(object sender, string error, string? state)
    {
        Console.WriteLine($"Aborting authorization, error received: {error}");
        await _server!.Stop();
    }

    private static async Task StartAuthentication()
    {

        var (verifier, challenge) = PKCEUtil.GenerateCodes();
        _server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);
        await _server.Start();
        _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
        _server.ErrorReceived += OnErrorReceived;

        var request = new LoginRequest(_server.BaseUri, _settings.ClientId!, LoginRequest.ResponseType.Code)
        {
            Scope = new List<string> { UserReadEmail, UserReadPrivate, PlaylistReadPrivate, UserLibraryRead, PlaylistModifyPrivate, PlaylistModifyPublic }
        };

        BrowserUtil.Open(request.ToUri());
    }
}

