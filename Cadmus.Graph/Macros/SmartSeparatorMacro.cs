using Fusi.Tools.Config;
using System;

namespace Cadmus.Graph.Macros
{
    /// <summary>
    /// Smart separator node mapping macro. This macro returns the specified
    /// separator if the placeholder being processed is not immediately
    /// preceded by it; otherwise, it returns an empty string.
    /// <para>Arguments: separator. If not specified, it defaults to a slash.
    /// </para>
    /// <para>Tag: <c>node-mapping-macro.smart-separator</c>.</para>
    /// </summary>
    [Tag("node-mapping-macro.smart-separator")]
    public sealed class SmartSeparatorMacro : INodeMappingMacro
    {
        private static bool HasSeparatorAt(string text, string sep, int index)
        {
            if (index < sep.Length) return false;
            for (int i = index; i < index + sep.Length; i++)
                if (text[i] != sep[i]) return false;
            return true;
        }

        /// <summary>
        /// Run the macro function.
        /// </summary>
        /// <param name="context">The data context of the macro function.</param>
        /// <param name="template">The template being processed.</param>
        /// <param name="index">The index to the macro placeholder in
        /// <paramref name="template"/>.</param>
        /// <param name="args">The optional arguments. This is a simple array
        /// of tokens, whose meaning depends on the function implementation.</param>
        /// <returns>Result or null.</returns>
        /// <exception cref="ArgumentNullException">template</exception>
        public string? Run(object? context, string template, int index,
            string[]? args)
        {
            if (template is null)
                throw new ArgumentNullException(nameof(template));

            string sep = args == null || args.Length == 0? "/" : args[0];
            return HasSeparatorAt(template, sep, index) ? "" : sep;
        }
    }
}
