using System.ComponentModel;

namespace Publiceringsverktyg.Models
{
    public class MatchResultViewModel
    {
        
        
        [DisplayName("Matchningsnivå")]
        public int MatchLevel { get; set; }

        public int BatchNumber { get; set; }
    }
}
