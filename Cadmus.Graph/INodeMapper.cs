using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Cadmus.Graph
{
    public interface INodeMapper : IHasDataDictionary
    {
        /// <summary>
        /// Gets or sets the optional logger to use.
        /// </summary>
        ILogger? Logger { get; set; }

        void SetMacros(IDictionary<string, INodeMappingMacro>? macros);

        void Map(object source, NodeMapping mapping, GraphSet target);
    }
}
