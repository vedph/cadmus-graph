﻿namespace Cadmus.Graph
{
    /// <summary>
    /// A graph's node.
    /// </summary>
    public class Node
    {
        /// <summary>
        /// The "user" value for <see cref="SourceType"/>.
        /// </summary>
        public const int SOURCE_USER = 0;
        /// <summary>
        /// The "item" value for <see cref="SourceType"/>.
        /// </summary>
        public const int SOURCE_ITEM = 1;
        /// <summary>
        /// The "part" value for <see cref="SourceType"/>.
        /// </summary>
        public const int SOURCE_PART = 2;
        /// <summary>
        /// The "thesaurus" value for <see cref="SourceType"/>.
        /// </summary>
        public const int SOURCE_THESAURUS = 3;

        /// <summary>
        /// The value for <see cref="Tag"/> when the node represents a property,
        /// i.e. a resource which can be used as a predicate.
        /// </summary>
        public const string TAG_PROPERTY = "property";

        /// <summary>
        /// Gets or sets the node's identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this node is a class.
        /// This is a shortcut property for a node being the subject of a triple
        /// with S=class URI, predicate="a" and object=rdfs:Class (or eventually
        /// owl:Class -- note that owl:Class is defined as a subclass of
        /// rdfs:Class).
        /// </summary>
        public bool IsClass { get; set; }

        /// <summary>
        /// Gets or sets the tag, used as a generic classification for nodes.
        /// For instance, this can be used to mark all the nodes potentially
        /// used as properties, so that a frontend can filter them accordingly.
        /// </summary>
        public string? Tag { get; set; }

        /// <summary>
        /// Gets or sets the optional node's label. Most nodes have a label
        /// to ease their editing.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Gets or sets the type of the source for this node.
        /// </summary>
        public int SourceType { get; set; }

        /// <summary>
        /// Gets or sets the source ID for this node.
        /// </summary>
        public string? Sid { get; set; }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"#{Id} {Label} [{SourceType}] {Sid}";
        }
    }
}
