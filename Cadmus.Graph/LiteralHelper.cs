using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Graph
{
    /// <summary>
    /// Helper for triple's literal value handling.
    /// </summary>
    public static class LiteralHelper
    {
        private static readonly Regex _litRegex =
            new(@"(?:(?:\^\^(?<t>.+))|(?:\@(?<l>[a-z]+)))?$",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses the literal value of the specified triple, adjusting it
        /// accordingly.
        /// </summary>
        /// <param name="triple">The triple.</param>
        /// <exception cref="ArgumentNullException">triple</exception>
        public static void AdjustLiteral(Triple triple)
        {
            if (triple is null)
                throw new ArgumentNullException(nameof(triple));

            if (triple.ObjectLiteral == null) return;

            // parse ^^type or @lang, removing them from the literal itself
            Match m = _litRegex.Match(triple.ObjectLiteral);
            if (m.Success)
            {
                // remove suffix
                triple.ObjectLiteral = triple.ObjectLiteral[..m.Index];
                // unwrap from ""
                triple.ObjectLiteral = triple.ObjectLiteral.Trim('"');

                // handle compatible numeric types
                if (m.Groups["t"].Length > 0)
                {
                    triple.LiteralType = m.Groups["t"].Value;
                    // from https://docs.microsoft.com/en-us/dotnet/standard/data/xml/mapping-xml-data-types-to-clr-types
                    switch (triple.LiteralType)
                    {
                        case "xs:boolean":
                            triple.LiteralNumber =
                                Convert.ToBoolean(triple.ObjectLiteral) ? 1 : 0;
                            break;
                        case "xs:byte":
                        case "xs:short":
                        case "xs:int":
                        case "xs:integer":
                        case "xs:long":
                        case "xs:float":
                        case "xs:double":
                        case "xs:unsignedByte":
                        case "xs:unsignedShort":
                        case "xs:unsignedInt":
                        case "xs:unsignedLong":
                            triple.LiteralNumber =
                                Convert.ToDouble(triple.ObjectLiteral);
                            break;
                    }
                }
                else if (m.Groups["l"].Length > 0)
                    triple.LiteralLanguage = m.Groups["l"].Value;
            }
            else triple.ObjectLiteral = triple.ObjectLiteral.Trim('"');

            // filter
            StringBuilder sb = new(triple.ObjectLiteral.Length);
            foreach (char c in triple.ObjectLiteral)
            {
                if (char.IsLetter(c)) sb.Append(UidFilter.GetSegment(c));
                else if (c == '\'' || char.IsDigit(c) || char.IsWhiteSpace(c))
                    sb.Append(c);
            }
            triple.ObjectLiteralIx = sb.ToString();
        }
    }
}
