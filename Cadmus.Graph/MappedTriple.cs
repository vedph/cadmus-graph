using System;

namespace Cadmus.Graph;

/// <summary>
/// A triple defined in a <see cref="NodeMapping"/>.
/// </summary>
public class MappedTriple
{
    /// <summary>
    /// Subject URI template.
    /// </summary>
    public string? S { get; set; }

    /// <summary>
    /// Predicate URI template.
    /// </summary>
    public string? P { get; set; }

    /// <summary>
    /// Object URI template.
    /// </summary>
    public string? O { get; set; }

    /// <summary>
    /// Object literal template.
    /// </summary>
    public string? OL { get; set; }

    /// <summary>
    /// Parses the specified text as a mapped triple.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Triple or null if invalid.</returns>
    public static MappedTriple? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // S
        int i = text.IndexOf(' ');
        if (i == -1) return null;
        string s = text[..i];

        // P
        while (i < text.Length && text[i] == ' ') i++;
        int pi = i;
        i = text.IndexOf(' ', pi);
        if (i == -1) return null;
        string p = text[pi..i];

        // O
        while (i < text.Length && text[i] == ' ') i++;
        if (i == text.Length) return null;
        string o = text[i..];
        return o.StartsWith("\"", StringComparison.Ordinal)
            ? new MappedTriple
            {
                S = s,
                P = p,
                OL = o
            }
            : new MappedTriple
            {
                S = s,
                P = p,
                O = o
            };
    }

    public override string ToString()
    {
        return $"{S} {P} {OL ?? O}";
    }
}
