// DelayedPaginator.cs
#if !NETSTANDARD2_0
using System.Runtime.CompilerServices;
#endif
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace YourNamespace
{
    /// <summary>
    /// A paginator that inserts a small delay between page requests to reduce the risk of 429s.
    /// Implements IPaginator directly (SimplePaginator methods are not virtual).
    /// </summary>
    public class DelayedPaginator : IPaginator
    {
        private readonly TimeSpan _interPageDelay;

        /// <param name="interPageDelay">Delay before fetching each subsequent page (default: 100ms).</param>
        public DelayedPaginator(TimeSpan? interPageDelay = null)
        {
            _interPageDelay = interPageDelay ?? TimeSpan.FromMilliseconds(100);
        }

        // Optional hooks to allow early stopping logic similar to SimplePaginator
        protected virtual Task<bool> ShouldContinue<T>(List<T> results, IPaginatable<T> page)
            => Task.FromResult(true);

        protected virtual Task<bool> ShouldContinue<T, TNext>(List<T> results, IPaginatable<T, TNext> page)
            => Task.FromResult(true);

        public async Task<IList<T>> PaginateAll<T>(
            IPaginatable<T> firstPage,
            IAPIConnector connector,
            CancellationToken cancel = default)
        {
            if (firstPage is null) throw new ArgumentNullException(nameof(firstPage));
            if (connector is null) throw new ArgumentNullException(nameof(connector));

            var page = firstPage;
            var results = new List<T>();

            if (page.Items != null)
                results.AddRange(page.Items);

            while (page.Next != null && await ShouldContinue(results, page).ConfigureAwait(false))
            {
                if (_interPageDelay > TimeSpan.Zero)
                    await Task.Delay(_interPageDelay, cancel).ConfigureAwait(false);

                page = await connector
                    .Get<Paging<T>>(new Uri(page.Next, UriKind.Absolute), cancel)
                    .ConfigureAwait(false);

                if (page.Items != null)
                    results.AddRange(page.Items);
            }

            return results;
        }

        public async Task<IList<T>> PaginateAll<T, TNext>(
            IPaginatable<T, TNext> firstPage,
            Func<TNext, IPaginatable<T, TNext>> mapper,
            IAPIConnector connector,
            CancellationToken cancel = default)
        {
            if (firstPage is null) throw new ArgumentNullException(nameof(firstPage));
            if (mapper is null) throw new ArgumentNullException(nameof(mapper));
            if (connector is null) throw new ArgumentNullException(nameof(connector));

            var page = firstPage;
            var results = new List<T>();

            if (page.Items != null)
                results.AddRange(page.Items);

            while (page.Next != null && await ShouldContinue(results, page).ConfigureAwait(false))
            {
                if (_interPageDelay > TimeSpan.Zero)
                    await Task.Delay(_interPageDelay, cancel).ConfigureAwait(false);

                var next = await connector
                    .Get<TNext>(new Uri(page.Next, UriKind.Absolute), cancel)
                    .ConfigureAwait(false);

                page = mapper(next);

                if (page.Items != null)
                    results.AddRange(page.Items);
            }

            return results;
        }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
        public async IAsyncEnumerable<T> Paginate<T>(
            IPaginatable<T> firstPage,
            IAPIConnector connector,
            [EnumeratorCancellation] CancellationToken cancel = default)
        {
            if (firstPage is null) throw new ArgumentNullException(nameof(firstPage));
            if (connector is null) throw new ArgumentNullException(nameof(connector));
            if (firstPage.Items == null)
                throw new ArgumentException("The first page has to contain an Items list!", nameof(firstPage));

            var page = firstPage;

            foreach (var item in page.Items)
                yield return item;

            while (page.Next != null)
            {
                if (_interPageDelay > TimeSpan.Zero)
                    await Task.Delay(_interPageDelay, cancel).ConfigureAwait(false);

                page = await connector
                    .Get<Paging<T>>(new Uri(page.Next, UriKind.Absolute), cancel)
                    .ConfigureAwait(false);

                foreach (var item in page.Items!)
                    yield return item;
            }
        }

        public async IAsyncEnumerable<T> Paginate<T, TNext>(
            IPaginatable<T, TNext> firstPage,
            Func<TNext, IPaginatable<T, TNext>> mapper,
            IAPIConnector connector,
            [EnumeratorCancellation] CancellationToken cancel = default)
        {
            if (firstPage is null) throw new ArgumentNullException(nameof(firstPage));
            if (mapper is null) throw new ArgumentNullException(nameof(mapper));
            if (connector is null) throw new ArgumentNullException(nameof(connector));
            if (firstPage.Items == null)
                throw new ArgumentException("The first page has to contain an Items list!", nameof(firstPage));

            var page = firstPage;

            foreach (var item in page.Items)
                yield return item;

            while (page.Next != null)
            {
                if (_interPageDelay > TimeSpan.Zero)
                    await Task.Delay(_interPageDelay, cancel).ConfigureAwait(false);

                var next = await connector
                    .Get<TNext>(new Uri(page.Next, UriKind.Absolute), cancel)
                    .ConfigureAwait(false);

                page = mapper(next);

                foreach (var item in page.Items!)
                    yield return item;
            }
        }
#endif
    }
}