using Fusi.Tools.Config;

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
        public string Id => "_smart-sep";

        private static bool HasSeparatorAt(string text, string sep, int index)
        {
            if (index < sep.Length) return false;
            for (int i = index; i < index + sep.Length; i++)
                if (text[i] != sep[i]) return false;
            return true;
        }

        public string? Run(object? context, string template, int index,
            string[]? args)
        {
            string sep = args == null || args.Length == 0? "/" : args[0];
            return HasSeparatorAt(template, sep, index) ? "" : sep;
        }
    }
}
