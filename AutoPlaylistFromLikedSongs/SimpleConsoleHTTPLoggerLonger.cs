using SpotifyAPI.Web.Http;

namespace AutoPlaylistFromLikedSongs
{
    public class SimpleConsoleHTTPLoggerLonger : IHTTPLogger
    {
        private const string OnRequestFormat = "\n{0} {1} [{2}] {3}";

        public void OnRequest(IRequest request)
        {
            if (request != null)
            {
                string? text = null;
                if (request.Parameters != null)
                {
                    text = string.Join(",", request.Parameters.Select<KeyValuePair<string, string>, string>((KeyValuePair<string, string> kv) => kv.Key + "=" + kv.Value)?.ToArray() ?? Array.Empty<string>());
                }

                Console.WriteLine("\n{0} {1} [{2}] {3}", request.Method, request.Endpoint, text, request.Body);
            }
        }

        public void OnResponse(IResponse response)
        {
            if (response != null)
            {
                string? text = response.Body?.ToString()?.Replace("\n", "", StringComparison.InvariantCulture);
                text = text?.Substring(0, Math.Min(250, text.Length));
                Console.WriteLine("--> {0} {1} {2}\n", response.StatusCode, response.ContentType, text);
            }
        }
    }
}