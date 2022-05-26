namespace Cadmus.Graph
{
    /// <summary>
    /// The result of searching a property.
    /// </summary>
    /// <seealso cref="Property" />
    public class PropertyResult : Property
    {
        /// <summary>
        /// Gets or sets the property URI.
        /// </summary>
        public string? Uri { get; set; }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return Uri ?? base.ToString()!;
        }
    }
}
