using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using DiVAImporterLibrary;
using System.Net;
using System.Text;
using Microsoft.SqlServer.Server;
using Publiceringsverktyg.Models;
using System.Xml.Linq;
using System.Xml;

namespace Publiceringsverktyg.Controllers
{
    public class WoSImportingController : Controller
    {
        string path = "~/App_Data/WosFiles";

        [HttpGet]
        public ActionResult Index(string SelectedFileName)
        {
            if (string.IsNullOrEmpty(SelectedFileName))
            {
                var dir = Directory.EnumerateFiles(Server.MapPath(path)).Select(a => a.Substring(a.LastIndexOf('\\') + 1));
                ViewBag.Files = dir;
                ViewBag.Title = "Ladda upp poster från WoS";
                return View();
            }
            else
            {
                var filePath = Server.MapPath(path + "\\" + SelectedFileName);
                var f = new FileInfo(filePath);
                Session["WosRecords"] = null;
                using (var sr = new StreamReader(filePath))
                {
                    Dictionary<string, string>[] wosRecords = MatchWoSToDiva.PrepareWosFile(sr).ToArray();
                    Session["WosRecords"] = wosRecords;
                }

                return RedirectToAction("PostDiVAFile");
            }

        }
        [HttpPost]
        public ActionResult Index(HttpPostedFileBase WoSFile)
        {
           

            Session["WosRecords"] = null;
            using (var sr = new StreamReader(WoSFile.InputStream))
            {
                Dictionary<string, string>[] wosRecords = MatchWoSToDiva.PrepareWosFile(sr).ToArray();
                Session["WosRecords"] = wosRecords;
            }

            return RedirectToAction("PostDiVAFile");

        }
        // GET: /WoSImporting/


        public ActionResult PostDiVAFile()
        {

            ViewBag.Title = "Ladda upp poster från DiVA";
            ViewBag.WosRecords = (Session["WosRecords"] != null)?((Dictionary<string, string>[])Session["WosRecords"]).Length : 0;
            //ToDo: Count and display number of WoS-records from previous step...
            ViewBag.YearFrom = DateTime.Now.Year - 1;
            ViewBag.YearTo = DateTime.Now.Year;
            return View();
        }
        [HttpPost]
        public ActionResult PostDiVAFile(HttpPostedFileBase divaFile)
        {
            ViewBag.YearFrom = DateTime.Now.Year - 1;
            ViewBag.YearTo = DateTime.Now.Year;
            Session["DivaRecords"] = null;

            using (var fileStream = divaFile.InputStream)
            {
                Dictionary<string, string>[] divaRecords = MatchWoSToDiva.PrepareDivaFile(new StreamReader(fileStream), true).ToArray();
                Session["DivaRecords"] = divaRecords;
            }
            return RedirectToAction("MatchingSplash");
        }

        [HttpPost]
        public ActionResult FetchFromDivaApi(int yearFrom, int yearTo)
        {

            var request = WebRequest.Create(
               String.Format(
                   ConfigurationManager.AppSettings["DivaApiUrl"],
                   yearFrom,
                   yearTo)
                   );
            request.ContentType = "text/csv";
            request.Method = "GET";
            using (var response = request.GetResponse() as HttpWebResponse)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    ViewBag.Error = string.Format("Error fetching data. Server returned status code: {0}", response.StatusCode);
                }
                else
                {
                    Dictionary<string, string>[] divaRecords = MatchWoSToDiva.PrepareDivaFile(new StreamReader(response.GetResponseStream()), false).ToArray();
                    Session["DivaRecords"] = divaRecords;
                }
            }
            return RedirectToAction("MatchingSplash");
        }
        public ActionResult MatchingSplash()
        {
            ViewBag.Title = "Klart för matchning";
            ViewBag.WosRecords = Session["WosRecords"];
            ViewBag.DivaRecords = Session["DivaRecords"];
            return View();
        }
        public ActionResult MatchResults()
        {
            ViewBag.Title = "Resultat av matchningen";
            var wosRecords = (Dictionary<string, string>[])Session["WosRecords"];
            var divaRecords = (Dictionary<string, string>[])Session["DivaRecords"];
            Tuple<Dictionary<string, string>, Dictionary<string, string>, int>[] matchResult
                = MatchWoSToDiva.StartMatching(divaRecords, wosRecords).ToArray();

            string[] matchedWosIds = matchResult.Select(a => a.Item2["UT"]).ToArray();
            string[] matchedDivaIds = matchResult.Select(a => a.Item1["PID"]).ToArray();
            Session["MatchResult"] = matchResult;

            //Get List of unmatched WoS records
            ViewBag.UnmatchedWos = wosRecords.Where(a => !matchedWosIds.Contains(a["UT"]));

            //Get List of unmatched DiVA records
            ViewBag.UnmatchedDivas = divaRecords.Where(a => !matchedDivaIds.Contains(a["PID"]));
            ViewBag.MatchResult = matchResult;
            var viewModel = new MatchResultViewModel { MatchLevel = -1 };
            return View(viewModel);
        }

        [HttpGet]
        public FileContentResult DownLoadWoSRecords(int batch, int matchLevel, string format = null)
        {
            int batchSize = int.Parse(ConfigurationManager.AppSettings["DownloadBatchSize"]);
            var wosRecords = (Dictionary<string, string>[]) Session["WosRecords"];
            var divaRecords = (Dictionary<string, string>[]) Session["DivaRecords"];
            var matchResult =
                (Tuple<Dictionary<string, string>, Dictionary<string, string>, int>[]) Session["MatchResult"];

            var matchedWosIds = matchResult.Select(a => a.Item2["UT"]).ToArray();
            var matchedDivaIds = matchResult.Select(a => a.Item1["PID"]).ToArray();

            //Get List of unmatched WoS records
            var unmatchedWos = wosRecords.Where(a => !matchedWosIds.Contains(a["UT"]));

            //Get List of unmatched DiVA records
            var unmatchedDivas = divaRecords.Where(a => !matchedDivaIds.Contains(a["PID"]));
            var downloadLevel = false;
            IEnumerable<Dictionary<string, string>> postsToDownload;
            if (matchLevel > 0)
            {
                postsToDownload = matchResult.Where(a => a.Item3 == matchLevel).Select(a => a.Item2);
                downloadLevel = true;
            }
            else
            {
                postsToDownload = unmatchedWos;
            }
            var posts = postsToDownload.Skip(batch * batchSize);
            if (posts.Count() >= batchSize)
                posts = posts.Take(batchSize);

             if(format == "MODS"){
                 XNamespace xmlns = "http://www.loc.gov/mods/v3";
                 XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
                 XNamespace xlink = "http://www.w3.org/1999/xlink";
                 XNamespace schemaLocation = "http://www.loc.gov/mods/v3 http://www.loc.gov/standards/mods/v3/mods-3-2.xsd";
                 // Konverterar postvis, resultatet ska bäddas in i ModsCollection.
                 var modsCollection = new XElement(xmlns + "modsCollection",
                     new XAttribute("xmlns", "http://www.loc.gov/mods/v3"),
                     new XAttribute("version", "3.2"),
                     new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                     new XAttribute(XNamespace.Xmlns + "xlink", "http://www.w3.org/1999/xlink"),
                     new XAttribute(xsi + "schemaLocation", "http://www.loc.gov/mods/v3 http://www.loc.gov/standards/mods/v3/mods-3-2.xsd"));
            foreach (var wospost in posts)
            {
                     modsCollection.Add(
                  WoSRecordWriter.ModsRecord(
                    wospost.Where(a => !a.Key.StartsWith("new") && a.Key != "FA").ToDictionary(a => a.Key, a => a.Value),
                         int.Parse(ConfigurationManager.AppSettings["MaxAuthorCount"]),ConfigurationManager.AppSettings["OnlyFirstLastAndLocalAuthors"] == "1")
                 );
              }
                 return File(new UTF8Encoding().GetBytes(modsCollection.ToString()), "text/XML", "Unmatched-" + (downloadLevel ? "level" + matchLevel + "-" : string.Empty) + DateTime.Now.ToShortDateString() + ".xml");
             }
             else
             {
                var s = new StringBuilder();
                foreach (var wospost in posts)
                {
                s.Append(
                    WoSRecordWriter.WoSRecordString(
                    wospost.Where(a => !a.Key.StartsWith("new") && a.Key != "FA").ToDictionary(a => a.Key, a => a.Value),
                    int.Parse(ConfigurationManager.AppSettings["MaxAuthorCount"]),
                    ConfigurationManager.AppSettings["OnlyFirstLastAndLocalAuthors"] == "1")
                    );
            }
            s.Append("\nEF");
            return File(new UTF8Encoding().GetBytes(s.ToString()), "text/csv", "Unmatched-" + (downloadLevel ? "level" + matchLevel + "-" : string.Empty) + DateTime.Now.ToShortDateString() + ".csv");
             }
        }

        // Test-controller för Wos-Mods-överföring
        // Filnamn: article.txt savedrecs.txt testsearch.txt WoS_SU_2011_ar-re_1-88_new_120227.txt
        public ActionResult WosToMods(string id)
        {
            var filePath = Server.MapPath(path + "\\" + (string.IsNullOrWhiteSpace(id) ? "testsearch4.txt" : id + ".txt"));
            var f = new FileInfo(filePath);
            Dictionary<string, string>[] wosRecords = null;
            using (var sr = new StreamReader(filePath))
            {
                wosRecords = MatchWoSToDiva.PrepareWosFile(sr).ToArray();
            }
            XNamespace xmlns = "http://www.loc.gov/mods/v3";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            XNamespace xlink = "http://www.w3.org/1999/xlink";
            XNamespace schemaLocation = "http://www.loc.gov/mods/v3 http://www.loc.gov/standards/mods/v3/mods-3-2.xsd";
            // Konverterar postvis, resultatet ska bäddas in i ModsCollection.
            var modsCollection = new XElement(xmlns + "modsCollection",
                new XAttribute("xmlns", "http://www.loc.gov/mods/v3"),
                new XAttribute("version", "3.2"),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Xmlns + "xlink", "http://www.w3.org/1999/xlink"),
                new XAttribute(xsi + "schemaLocation", "http://www.loc.gov/mods/v3 http://www.loc.gov/standards/mods/v3/mods-3-2.xsd"));
            foreach (var wosRecord in wosRecords){
                modsCollection.Add(WoSRecordWriter.ModsRecord(wosRecord, int.Parse(ConfigurationManager.AppSettings["MaxAuthorCount"]), ConfigurationManager.AppSettings["OnlyFirstLastAndLocalAuthors"] == "1"));
            }
            return new XmlActionResult(modsCollection);
        }

        public ActionResult EditSettings()
        {
            var configuration = WebConfigurationManager.OpenWebConfiguration("~");
            var appSettings = configuration.AppSettings.Settings;
            var settings = new WosImportingSettings()
            {
                MaxAuthorCount = int.Parse(appSettings["MaxAuthorCount"].Value),
                DownloadBatchSize = int.Parse(appSettings["DownloadBatchSize"].Value),
                OwnAuthorAffiliationRegExp = appSettings["OwnAuthorAffiliationRegExp"].Value,
                DivaApiUrl = appSettings["DivaApiUrl"].Value,
                OnlyFirstLastAndLocalAuthors = appSettings["OnlyFirstLastAndLocalAuthors"].Value == "1"

            };
            return View(settings);
        }

        [HttpPost]
        public ActionResult EditSettings(WosImportingSettings settings)
        {
            var configuration = WebConfigurationManager.OpenWebConfiguration("~");
            var appSettings = configuration.AppSettings.Settings;
            appSettings["MaxAuthorCount"].Value = settings.MaxAuthorCount.ToString();
            appSettings["DownloadBatchSize"].Value = settings.DownloadBatchSize.ToString();
            appSettings["OwnAuthorAffiliationRegExp"].Value = settings.OwnAuthorAffiliationRegExp;
            appSettings["OnlyFirstLastAndLocalAuthors"].Value = settings.OnlyFirstLastAndLocalAuthors ? "1" : "0";
            appSettings["DivaApiUrl"].Value = settings.DivaApiUrl;
            configuration.Save();
            return RedirectToAction("Index");
        }

    }
    // Temporärt för att testa MODS-konverteringen
    public sealed class XmlActionResult : ActionResult
    {
        private readonly XDocument _document;

        public Formatting Formatting { get; set; }
        public string MimeType { get; set; }

        public XmlActionResult(XElement element)
        {
            var document = new XDocument(element);
            if (document == null)
                throw new ArgumentNullException("document");

            _document = document;

            // Default values
            MimeType = "text/xml";
            Formatting = Formatting.None;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            context.HttpContext.Response.Clear();
            context.HttpContext.Response.ContentType = MimeType;

            using (var writer = new XmlTextWriter(context.HttpContext.Response.OutputStream, Encoding.UTF8) { Formatting = Formatting })
                _document.WriteTo(writer);
        }
    }
}
