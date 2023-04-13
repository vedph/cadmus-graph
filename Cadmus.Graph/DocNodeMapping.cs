namespace Cadmus.Graph;

/// <summary>
/// A node mapping in a document, which can be either a reference to a named
/// mapping, or an inline mapping. This is used in <see cref="NodeMappingDocument"/>
/// to avoid repetitions in the document.
/// </summary>
public class DocNodeMapping
{
    /// <summary>
    /// Gets or sets the inline mapping value.
    /// </summary>
    public NodeMapping? Value { get; set; }

    /// <summary>
    /// Gets or sets the reference identifier to a named mapping.
    /// </summary>
    public string? ReferenceId { get; set; }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return Value?.ToString() ?? ReferenceId ?? base.ToString()!;
    }
}
