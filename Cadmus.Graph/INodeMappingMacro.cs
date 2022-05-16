namespace Cadmus.Graph
{
    /// <summary>
    /// Node mapping macro function.
    /// </summary>
    public interface INodeMappingMacro
    {
        /// <summary>
        /// The macro ID.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Run the macro function.
        /// </summary>
        /// <param name="context">The data context of the macro function.</param>
        /// <returns>Result or null.</returns>
        string? Run(object? context);
    }
}
