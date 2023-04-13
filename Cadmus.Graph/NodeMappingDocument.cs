using System.Collections.Generic;

namespace Cadmus.Graph;

/// <summary>
/// A document including node mappings. This is a utility class used to
/// load a set of mappings from a JSON file, while avoiding repetitions in it
/// via named references. Typically, a document is loaded from a JSON stream
/// or string by just deserializing it into this class. Then you can use
/// <see cref="GetMappings"/> to get all the mappings in the document properly
/// dereferenced.
/// </summary>
public class NodeMappingDocument
{
    /// <summary>
    /// Gets or sets the named mappings. These mappings are reused across
    /// the document via their names.
    /// </summary>
    public Dictionary<string, NodeMapping> NamedMappings { get; set; }

    /// <summary>
    /// Gets or sets the mappings in the document. These can be either
    /// references to named mappings, or inline mappings.
    /// </summary>
    public List<DocNodeMapping> DocumentMappings { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeMappingDocument"/> class.
    /// </summary>
    public NodeMappingDocument()
    {
        NamedMappings = new();
        DocumentMappings = new();
    }

    /// <summary>
    /// Gets all the document's mappings, dereferencing those which are not
    /// inlined. Note that multiple references to the same mapping will return
    /// the same mapping object multiple times.
    /// </summary>
    /// <param name="ignoreMissingRefs">True to ignore any missing references,
    /// false to throw an exception.</param>
    /// <returns>Mappings.</returns>
    /// <exception cref="CadmusGraphException">Reference to unknown mapping
    /// (when <paramref name="ignoreMissingRefs"/> is false)</exception>
    public IEnumerable<NodeMapping> GetMappings(bool ignoreMissingRefs = false)
    {
        foreach (DocNodeMapping mapping in DocumentMappings)
        {
            if (mapping.Value == null)
            {
                // ignore an empty mapping
                if (mapping.ReferenceId == null) continue;

                // dereference a mapping reference
                if (NamedMappings.TryGetValue(mapping.ReferenceId,
                    out NodeMapping? m))
                {
                    yield return m;
                }
                // throw error if cannot be dereferenced and we're not ignoring this
                else if (!ignoreMissingRefs)
                {
                    throw new CadmusGraphException(
                        $"Reference to unknown mapping: \"{mapping.ReferenceId}\"");
                }
            }
            else
            {
                // just return an inline mapping
                yield return mapping.Value;
            }
        }
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"Named: {NamedMappings.Count} - Document: {DocumentMappings.Count}";
    }
}
