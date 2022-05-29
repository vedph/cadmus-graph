using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Cadmus.Graph
{
    /// <summary>
    /// RAM-based UID builder. This is essentially used for diagnostic or
    /// demo purposes.
    /// </summary>
    public sealed class RamUidBuilder : IUidBuilder
    {
        private static int _nextId;
        private static readonly ConcurrentDictionary
            <Tuple<string, string>, int> _lookup = new();
        private static readonly ConcurrentDictionary<string, bool>
            _unsuffixed = new();

        /// <summary>
        /// Build the eventually suffixed UID.
        /// </summary>
        /// <param name="unsuffixed">The generated, unsuffixed UID.</param>
        /// <param name="sid">The source ID (SID).</param>
        /// <returns>UID, eventually suffixed with #N.</returns>
        /// <exception cref="ArgumentException">sid or unsuffixed</exception>
        public string BuildUid(string unsuffixed, string sid)
        {
            if (string.IsNullOrEmpty(sid))
                throw new ArgumentException(nameof(sid));
            if (string.IsNullOrEmpty(unsuffixed))
                throw new ArgumentException(nameof(unsuffixed));

            // if SID+UID exists, just return UID + its eventual suffix
            var key = new Tuple<string, string>(sid, unsuffixed);
            if (_lookup.ContainsKey(key))
            {
                return _lookup[key] == 0
                    ? unsuffixed
                    : $"{unsuffixed}#{_lookup[key]}";
            }

            // if unsuffixed exists, add as a new suffixed UID
            if (_unsuffixed.ContainsKey(unsuffixed))
            {
                int id = Interlocked.Increment(ref _nextId);
                _lookup[key] = id;
                return $"{unsuffixed}#{id}";
            }

            // else add unsuffixed
            _lookup[key] = 0;
            _unsuffixed[unsuffixed] = true;
            return unsuffixed;
        }
    }
}
