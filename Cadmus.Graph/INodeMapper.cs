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

        void SetMacros(IList<INodeMappingMacro>? macros);

        void Map(string sid, object source, NodeMapping mapping, GraphSet target);
    }
}
