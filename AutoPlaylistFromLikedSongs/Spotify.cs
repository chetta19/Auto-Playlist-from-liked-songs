using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPlaylistFromLikedSongs
{
    public class Spotify
    {
        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public bool PlaylistByYear { get; set; } = true;

        public bool PlaylistByGenre { get; set; } = true;

        public bool PlaylistByArtist { get; set; } = false;

        public bool PlaylistDiscoverAlbumOfLikedSongs { get; set; } = false;

        public int PlaylistDiscoverAlbumOfLikedSongsMinimumAlbumYear { get; set; } = 0;

        public int PlaylistByArtistMinimumLikedSongs { get; set; } = 0;
    }
}
