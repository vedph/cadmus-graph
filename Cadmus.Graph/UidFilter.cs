using System;
using System.Text;
using Fusi.Text.Unicode;

namespace Cadmus.Graph
{
    /// <summary>
    /// UID filter. This is used to adjust a generated UID so that it conforms
    /// to the conventions defined for them.
    /// </summary>
    public static class UidFilter
    {
        private readonly static Lazy<UniData> _ud = new(() => new UniData());

        /// <summary>
        /// Apply this UID filter to the specified UID value.
        /// This replaces whitespace with <c>_</c>, preserves only letters
        /// (lowercased and without diacritics), digits, and characters
        /// <c>-_#/&%=.?</c>. It also ensures that the UID is not empty,
        /// in this case replacing it with <c>_</c>.
        /// If the UID starts with <c>!</c>, no filters are applied except
        /// for removing the initial <c>!</c>.
        /// </summary>
        /// <param name="uid">the UID to apply this filter to.</param>
        /// <returns>Filtered UID.</returns>
        /// <exception cref="ArgumentNullException">uid</exception>
        public static string Apply(string uid)
        {
            if (uid is null) throw new ArgumentNullException(nameof(uid));

            // ensure the UID is not empty
            if (uid.Length == 0) return "_";

            // honor filter disable directive (initial !)
            if (uid[0] == '!')
            {
                uid = uid[1..];
                return uid.Length > 0? uid : "_";
            }

            // filter
            StringBuilder sb = new(uid.Length);
            foreach (char c in uid)
            {
                switch (c)
                {
                    case '_':
                    case '-':
                    case '#':
                    case '/':
                    case '&':
                    case '%':
                    case '=':
                    case '.':
                    case '?':
                        sb.Append(c);
                        break;
                    default:
                        if (char.IsLetter(c))
                        {
                            sb.Append(char.ToLowerInvariant(_ud.Value.GetSegment(c, true)));
                            break;
                        }
                        if (c >= '0' && c <= '9')
                        {
                            sb.Append(c);
                            break;
                        }
                        if (char.IsWhiteSpace(c)) sb.Append('_');
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
