using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace Publiceringsverktyg.Models
{
    public class WosImportingSettings
    {
        
        [DisplayName("Max antal författare")]
        public int MaxAuthorCount { get; set; }
        
        [DisplayName("Ta bara med första, sista och upphovsmännen från det egna lärosätet")]
        public bool OnlyFirstLastAndLocalAuthors { get; set; }

        [DisplayName("Reguljärt uttryck för affiliering av författare")]
        public string OwnAuthorAffiliationRegExp { get; set; }

        [DisplayName("Url för anrop till DiVAs API")]
        public string DivaApiUrl { get; set; }

        [DisplayName("Antal matchade poster att ladda ned åt gången")]
        public int DownloadBatchSize { get; set; }

    }
}