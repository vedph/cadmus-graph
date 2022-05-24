using Fusi.Antiquity.Chronology;
using Fusi.Tools.Config;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Cadmus.Graph.Macros
{
    /// <summary>
    /// Historical date macro. This parses a <see cref="HistoricalDate"/> from
    /// the received context, and returns either its sort value or its textual
    /// representation.
    /// <para>Tag: node-mapping-macro.historical-date.</para>
    /// </summary>
    [Tag("node-mapping-macro.historical-date")]
    public sealed class HistoricalDateMacro : INodeMappingMacro
    {
        private static readonly JsonSerializerOptions _options =
            new()
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
            };

        private static HistoricalDate? ParseDate(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<HistoricalDate>(json, _options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// Run the macro function.
        /// </summary>
        /// <param name="context">The data context of the macro function.</param>
        /// <param name="args">The optional arguments. This is a simple array
        /// of tokens, whose meaning depends on the function implementation.</param>
        /// <returns>Result or null.</returns>
        /// <exception cref="ArgumentNullException">template</exception>
        public string? Run(object? context, string[]? args)
        {
            HistoricalDate? date = ParseDate(context as string ?? "{}");
            if (args == null || args.Length == 0 || args[0] == "value")
            {
                return date?.GetSortValue()
                    .ToString(CultureInfo.InvariantCulture) ?? "0";
            }
            return date?.ToString() ?? "";
        }
    }
}
