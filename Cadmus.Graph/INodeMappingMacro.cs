namespace Cadmus.Graph
{
    /// <summary>
    /// Node mapping macro function.
    /// </summary>
    public interface INodeMappingMacro
    {
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
        string? Run(object? context, string template, int index, string[]? args);
    }
}
