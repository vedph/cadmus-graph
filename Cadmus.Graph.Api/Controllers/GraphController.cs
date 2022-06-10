using Cadmus.Graph.Api.Models;
using Fusi.Tools.Data;
using Microsoft.AspNetCore.Mvc;

namespace Cadmus.Graph.Api.Controllers
{
    //[Route("api/[controller]")]
    [ApiController]
    public sealed class GraphController : ControllerBase
    {
        private readonly IGraphRepository _repository;

        public GraphController(IGraphRepository repository)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
        }

        [HttpGet("api/graph/nodes/{id}", Name = "GetNode")]
        public ActionResult GetNode([FromRoute] int id)
        {
            UriNode? node = _repository.GetNode(id);
            if (node == null) return NotFound();
            return Ok(node);
        }

        [HttpGet("api/graph/walk/triples")]
        public DataPage<TripleGroup> GetTripleGroups([FromQuery]
            TripleFilterBindingModel model)
        {
            DataPage<TripleGroup> page = _repository.GetTripleGroups(
                model.ToTripleFilter(), model.Sort ?? "Cu");
            return page;
        }
    }
}
