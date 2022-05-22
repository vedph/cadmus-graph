using Cadmus.Graph;
using System.ComponentModel.DataAnnotations;

namespace CadmusGraphDemo.Pages
{
    public class DemoModel
    {
        [Required]
        public string? Input { get; set; }
        [Required]
        public string? Mappings { get; set; }
        public GraphSet? Graph { get; set; }
        public string? Error { get; set; }
        public bool IsRunning { get; set; }
    }
}
