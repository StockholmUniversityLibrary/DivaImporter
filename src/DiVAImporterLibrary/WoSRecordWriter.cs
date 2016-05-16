using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Deployment.Internal;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Xml.Linq;
using System.IO;
using System.Globalization;

namespace DiVAImporterLibrary
{
    public static class WoSRecordWriter
    {

        // Denna bör kanske brytas ut till en egen MODSRecordWriter-klass
        public static XElement ModsRecord(Dictionary<string, string> fields, int maxAuthorCount, bool onlyFirstLastAndLocalAuthors)
        {
            XNamespace xmlns = "http://www.loc.gov/mods/v3";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            XNamespace xlink = "http://www.w3.org/1999/xlink";
            XNamespace schemaLocation = "http://www.loc.gov/mods/v3 http://www.loc.gov/standards/mods/v3/mods-3-2.xsd";
            // Kom ihåg att resultatet ska bäddas in i ModsCollection.
            var mods = new XElement(xmlns + "mods",
                new XAttribute("xmlns", "http://www.loc.gov/mods/v3"),
                new XAttribute("version", "3.2"),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(XNamespace.Xmlns + "xlink", "http://www.w3.org/1999/xlink"),
                new XAttribute(xsi + "schemaLocation", "http://www.loc.gov/mods/v3 http://www.loc.gov/standards/mods/v3/mods-3-2.xsd"));
            // "Globala" värden samt metoder för aggregrat av olika WOS-taggar
            var langCode = FieldExists(fields, "LA") ? DetectLanguage(fields["LA"].ToLower()) : "swe";
            var langTerm = new XElement(xmlns + "languageTerm", langCode, new XAttribute("type", "code"), new XAttribute("authority", "iso639-2b"));
            var dtval = FieldExists(fields, "DT") ? fields["DT"].Trim().ToLower() : string.Empty;
            bool isArticle = dtval.Equals("article") ? true : false;
            if (dtval.Length > 0)
            {
                mods.Add(new XElement(xmlns + "genre", MapPublicationType(dtval),
                    new XAttribute("authority", "diva"),
                    new XAttribute("type", "publicationTypeCode")));
            }
            mods.Add(new XElement(xmlns + "genre", "refereed",
                    new XAttribute("authority", "diva"),
                    new XAttribute("type", "contentTypeCode")));
            ModsName(mods, fields, xmlns, maxAuthorCount);
            mods.Add(new XElement(xmlns + "language", langTerm));
            ModsOriginInfo(mods, fields, isArticle, xmlns);
            if (FieldExists(fields, "SN") && FieldExists(fields, "SO"))
            {
                ModsRelatedItem(mods, fields, xmlns, "host");
            }
            
            // Gå igenom övriga WOS-fält och mappa mot MODS
            foreach (KeyValuePair<string, string> field in fields.Where(field => field.Key.Trim() != string.Empty && field.Value.Trim() != string.Empty))
            {
                //mods.Add(new XElement(xmlns + field.Key, field.Value));
                switch (field.Key)
                {
                   case ("CA"):
                        mods.Add(new XElement(xmlns + "name", new XAttribute("type", "corporate"),
                            new XElement(xmlns + "namePart", field.Value),
                            new XElement(xmlns + "role", new XElement(xmlns + "roleTerm", new XAttribute("type", "code"), new XAttribute("authority", "marcrelator"), "oth")),
                            new XElement(xmlns + "description", "Research Group")));
                        break;
                    case "AR":
                        mods.Add(new XElement(xmlns + "identifier", field.Value,
                                    new XAttribute("type", "articleId")));
                        break;
                    case ("DI"):
                        mods.Add(new XElement(xmlns + "identifier", field.Value,
                                    new XAttribute("type", "doi")));
                        break;
                    case ("UT"):
                        mods.Add(new XElement(xmlns + "identifier", field.Value,
                                    new XAttribute("type", "isi")));
                        break;
                    case ("TI"):
                        mods.Add(ModsTitle(field.Value, xmlns, langCode));
                        break;
                    case ("AB"):
                        mods.Add(new XElement(xmlns + "abstract",
                            System.Web.HttpUtility.HtmlDecode(field.Value), new XAttribute("lang", langCode)));
                        break;
                    case ("DE"): // Inte ID (Keywords Plus)
                        foreach (var keyword in field.Value.Split(';'))
                        {
                            mods.Add(new XElement(xmlns + "subject",
                                new XElement(xmlns + "topic", keyword.Trim()),
                                new XAttribute("lang", "eng")));
                        }
                        break;
                    case ("WC"):
                        // Hämta HSV/WoS-mappningar från fil
                        //"PHYSICS, FLUIDS & PLASMAS": { "HSV 3-siffrig kod":103, "HSV namn":"Fysik"},
                        var jsonPath = System.Web.HttpContext.Current.Server.MapPath("~/App_Data/subjectMapping.json");
                        var json = File.ReadAllText(jsonPath);
                        dynamic mappings = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        foreach (var subject in field.Value.Split(';'))
                        {
                            var hsvSubject = MapHsvSubject(subject.ToUpper().Trim(), mappings);
                            if (!string.IsNullOrWhiteSpace(hsvSubject.Key) &&
                                !string.IsNullOrWhiteSpace(hsvSubject.Value))
                            {
                                var href = new XAttribute(xlink + "href", hsvSubject.Key);
                                var topic = new XElement(xmlns + "topic", hsvSubject.Value);
                                mods.Add(new XElement(xmlns + "subject", topic, new XAttribute("lang", "eng"),
                                    new XAttribute("authority", "hsv"), href));
                            }
                            else
                            {
                                mods.Add(new XElement(xmlns + "subject",
                                new XElement(xmlns + "topic", subject.Trim()),
                                new XAttribute("lang", "eng")));
                            }
                        }
                        break;
                }
            }
            return mods;
        }

        // Mappa LA
        private static string DetectLanguage(string la)
        {
            var langListSwe = "{'dan':'Danska','eng':'Engelska','emp':'                       ','esp':'Esperanto','est':'Estniska','ara':'Arabiska','far':'Färöiska','fin':'Finska','fre':'Franska','ger':'Tyska','gre':'Nygrekiska (1453-)','lit':'Litauiska','lat':'Latin','mac':'Makedonska','mad':'Madurese','mon':'Mongoliskt språk','bul':'Bulgariska','cat':'Katalanska','chi':'Kinesiska','cro':'Kroatiska','cze':'Tjeckiska','hin':'Hindi','heb':'Hebreiska','hun':'Ungerska','ind':'Indonesiska','ice':'Isländska','ita':'Italienska','iri':'Iriska','nob':'Bokmål','non':'Nynorsk','nor':'Norska','ned':'Nedeländska','tur':'Turkiska','urd':'Urdu','und':'Odefinierat språk','ukr':'Ukrainska','jap':'Japanska','kor':'Koreanska','kal':'Grönländska (Kalaallit oqaasi)','kur':'kurdiska','per':'Persiska','por':'Portogisiska','pol':'Polska','vie':'Vietnamesiska','rus':'Ryska','swe':'Svenska','spa':'Spanska','ser':'Serbiska','san':'Sanskrit','sam':'Samiskt språk','slova':'Slovakiska','slove':'Slovenska','latv':'Lettiska'}".ToLower();
            var langList = "{'dan':'Danish','dut':'Dutch; Flemish','eng':'English','emp':'                   ','esp':'Esparanto','est':'Estonian','ara':'Arabic','far':'Faroese','fin':'Finnish','fre':'French','ger':'German','gre':'Greek, Modern (1453-)','lit':'Lithuanian','lat':'Latin','mac':'Macedonian','mad':'Madurese','mon':'Mongolian','bul':'Bulgarian','cat':'Catalan; Valencian','chi':'Chinese','cze':'Czech','hin':'Hindi','heb':'Hebrew','hun':'Hungarian','ind':'Indonesian','ice':'Icelandic','ita':'Italian','iri':'Irish','nob':'Bokmål, Norwegian; Norwegian Bokmål','non':'Norwegian Nynorsk; Nynorsk Norwegian','nor':'Norwegian','tur':'Turkish','urd':'Urdu','und':'Undetermined','ukr':'Ukrainian','jap':'Japanese','kor':'Korean','kal':'Kalaallisut; Greenlandic','kur':'Kurdish','per':'Persian','por':'Portuguese','pol':'Polish','vie':'Vietnamese','rus':'Russian','swe':'Swedish','spa':'Spanish','ser':'Serbian','san':'Sanskrit','sam':'Sami languages (Other)','Cro':'Croatian','slova':'Slovak','slove':'Slovenian','latv':'Latvian'}".ToLower();
            var langJson = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(langList);
            if (langJson.ContainsValue(la))
            {
                return langJson.Where(kv => kv.Value.Equals(la)).Select(kv => kv.Key).FirstOrDefault();
            }
            return "swe";
        }
        // Mappa DT
        private static string MapPublicationType(string pt)
        {
            switch (pt)
            {
                case "article":
                    return "article";
                case "meeting abstract":
                    return "article";
                case "book chapter":
                    return "chapter";
                case "letter":
                    return "other";
                case "proceedings paper":
                    return "conferenceProceedings";
                case "review":
                    return "review";
                default:
                    return "other";
            }
        }
        // Mappa SC
        private static KeyValuePair<string, string> MapHsvSubject(string subj, dynamic mappings)
        {
            string hsvId;
            string hsvName;
            try
            {
                var hsvSub = mappings[subj];
                hsvId = (string)hsvSub["HSV 3-siffrig kod"] ?? string.Empty;
                hsvName = (string)hsvSub["HSV namn"] ?? string.Empty;
            }
            catch (Exception e)
            {
                hsvId = string.Empty;
                hsvName = string.Empty;
            }
            return new KeyValuePair<string, string>(hsvId, hsvName);
        }
        // Mappa PI, PU och PY (utgivarinformation)
        private static void ModsOriginInfo(XElement mods, Dictionary<string, string> fields, bool isArticle, XNamespace xmlns)
        {
            var place = FieldExists(fields, "PI") ? new XElement(xmlns + "place", new XElement(xmlns + "placeTerm", fields["PI"])) : null;
            var publisher = FieldExists(fields, "PU") ? new XElement(xmlns + "publisher", fields["PU"]) : null;
            var publicationYear = FieldExists(fields, "PY") ? new XElement(xmlns + "dateIssued", fields["PY"]) : null;
            if (isArticle)
            {
                mods.Add(new XElement(xmlns + "originInfo", publicationYear));
            }
            else
            {
                mods.Add(new XElement(xmlns + "originInfo", place, publicationYear, publisher));
            }
            
        }
        // Mappa VL, IS, BP, EP
        private static void ModsRelatedItem(XElement mods, Dictionary<string, string> fields, XNamespace xmlns, string type)
        {
            var volume = FieldExists(fields, "VL") ? new XElement(xmlns + "detail", new XElement(xmlns + "number", fields["VL"]), new XAttribute("type", "volume")) : null;
            // Använd "IS" (issue) istället för "AR" (article number)
            var issue = FieldExists(fields, "IS") ? new XElement(xmlns + "detail", new XElement(xmlns + "number", fields["IS"]), new XAttribute("type", "issue")) : null;
            var start = FieldExists(fields, "BP") ? new XElement(xmlns + "start", fields["BP"]) : null;
            var end = FieldExists(fields, "EP") ? new XElement(xmlns + "end", fields["EP"]) : null;
            var part = (volume == null && issue == null && start == null && end == null) ? null : new XElement(xmlns + "part", volume, issue, new XElement(xmlns + "extent", start, end));
            //var titleString = new CultureInfo("en-US").TextInfo.ToTitleCase(fields["SO"]);
            var titleString = fields["SO"];
            var titleInfo = ModsTitle(titleString, xmlns, null);
            mods.Add(new XElement(xmlns + "relatedItem",
                                new XAttribute("type", "host"),
                                titleInfo,
                                new XElement(xmlns + "identifier", fields["SN"],
                                    new XAttribute("type", "issn")),
                                    part));
        }
        // Helper för att skapa title/subtitle
        private static XElement ModsTitle(string titleString, XNamespace xmlns, string langCode = null)
        {
            var langAttr = langCode == null ? null : new XAttribute("lang", langCode);
            var ttmp = titleString.Split(':');
            if (ttmp.Length > 1)
            {
                var title = ttmp[0].Trim();
                var subTitle = ttmp[1].Trim();
                return new XElement(xmlns + "titleInfo", langAttr, new XElement(xmlns + "title", title), new XElement(xmlns + "subTitle", subTitle));
            }
            return new XElement(xmlns + "titleInfo", langAttr, new XElement(xmlns + "title", titleString));
        }
        // Mappa AF/AU och C1
        private static void ModsName(XElement mods, Dictionary<string, string> fields, XNamespace xmlns, int maxAuthorCount)
        {
            var noteBuilder = new StringBuilder();
            // Lägg författarna i AF eller AU i en sträng så att man inte behöver kolla båda varje gång
            var authorString = "";
            if (FieldExists(fields, "AF"))
            {
                authorString = fields["AF"];
            }
            // Försök annars med AU
            else if (FieldExists(fields, "AU"))
            {
                authorString = fields["AU"];
            }
            // Lägg författarna i AF eller AU i en lista
            var authorsInAfOrAu = authorString.Split(';');
            if (maxAuthorCount == 0)
            {
                maxAuthorCount = 250;
            }
            var maxAuthorsCondition = authorsInAfOrAu.Count() > maxAuthorCount; // Om antalet författare är större än max tillåtna antalet författare
            mods.Add(new XElement(xmlns + "note", authorsInAfOrAu.Length, new XAttribute("type", "creatorCount")));
            //mods.Add(new XElement(xmlns + "note", maxAuthorCount, new XAttribute("type", "maxAuthorCount")));
            // Gör om C1-fälten (endast de med SU-affiliering) till en dictionary 
            // där författaren är nyckeln och en array med tillhörande adresser är värdet
            var suAuthorsWithAddresses = new Dictionary<string, List<string>>();
            // Om adresslista finns
            if (FieldExists(fields, "C1"))
            {
                suAuthorsWithAddresses = GetSuAuthorsWithAddresses(fields["C1"], authorsInAfOrAu);
                // Skriv ut eventuella felmeddelanden i ett anmärksningsfält
                foreach (var suAuthor in suAuthorsWithAddresses.Where(a => a.Key.StartsWith("Felmeddelande")))
                {
                    var note = suAuthor.Key + " (" + suAuthor.Value.First() + ")";
                    noteBuilder.Append(noteBuilder.ToString().Length > 10 ? " --- " + note : note);
                }
            }

            // Om reprintadress finns
            if (FieldExists(fields, "RP"))
            {
                var reprintAddressWithAuthor = GetReprintAddressWithAuthor(fields["RP"]);
                var addr = reprintAddressWithAuthor.Key;
                var aut = reprintAddressWithAuthor.Value[0];
                // Kolla om addressen är SU
                var affInfo = Match(addr) ? "Affilierad reprint-författare: " : "Ej affilierad reprint-författare: ";
                var note = affInfo + aut + ", " + addr;
                noteBuilder.Append(noteBuilder.ToString().Length > 10 ? " --- " + note : note);
            }

            // Lägg till de affilierade författarna från adresslistan och ev. oaffilierade författare från AF/AU-listan 
            foreach (var auth in authorsInAfOrAu)
            {
                var authKey = auth.PrepareAuthor();
                if (suAuthorsWithAddresses.ContainsKey(authKey))
                {
                    var affAuth = suAuthorsWithAddresses[authKey];
                    CreateSuPersonElement(mods, authKey, affAuth, xmlns);
                    if (affAuth.Count > 1)
                    {
                        var note = "Dubbelaffiliering: " + authKey + " = " + string.Join("; ", affAuth);
                        noteBuilder.Append(noteBuilder.ToString().Length > 10 ? " --- " + note : note);
                    }
                }
                else
                {
                    if (!maxAuthorsCondition) // Om antalet författare överstiger det tillåtna antalet ska inte oaffilierade författare läggas in
                        CreateNonSuPersonElement(mods, auth, xmlns);
                }
            }
            // Lägg till anteckning om dubbelaffiliering etc.
            if (noteBuilder.ToString().Length > 0)
            {
                mods.Add(new XElement(xmlns + "note", noteBuilder.ToString()));
            }
            // Lägg till recordcount
        }

        /// <summary>
        /// Hämtar ut författarna med respektive affilieringar från C1
        /// </summary>
        /// <param name="authorsWithAddresses">WOS-formatets C1-tagg</param>
        /// <param name="authorsFromAfOrAu">Sträng med författarna från af/au</param>
        /// <returns>En dictionary med författaren som nyckel och en array med adresserna som värde</returns>
        private static Dictionary<string, List<string>> GetSuAuthorsWithAddresses(string authorsWithAddresses, string[] authorsFromAfOrAu)
        {
            var tmp = new Dictionary<string, List<string>>();
            // C1-taggen enligt följande mall: "[Efternamn1, Förnamn1; Efternamn2, Förnamn2...] Organisationsadress1; [Efternamn1, Förnamn1...] Organisationsadress2"
            // Alternativt endast organisationsadressen, i de fall publikationen bara har en författare
            authorsWithAddresses = authorsWithAddresses.Replace(";[", "; [");
            //System.Diagnostics.Debug.WriteLine("ANTALET ADRESS/FÖRFATTARE: " + authorsWithAddresses.Split(new[] { "; [" }, StringSplitOptions.None).Length);
            if (!authorsWithAddresses.Contains("[") && !authorsWithAddresses.Contains("]")) // Om det inte finns brackets i C1 är det bara en adress utan författarlista
            {
                //System.Diagnostics.Debug.WriteLine("No brackets in C1 = only one address");
                var address = authorsWithAddresses.Trim(new[] { ' ', '.', ',', '[', ']' });
                if (!Match(address))
                {
                    //System.Diagnostics.Debug.WriteLine("No matching SU affiliation = skip");
                    return tmp;
                }
                if (authorsFromAfOrAu.Length != 1) // Fler än en författare i AF/AU
                {
                    //System.Diagnostics.Debug.WriteLine("More authors in AF/SU than addresses in C1 = report");
                    tmp.Add("Felmeddelande: Antalet författare är fler än antalet adresser.", new List<string> { address }); // Lägg till ett felmeddelande som nyckel och adressen som enda värde i listan
                    return tmp;
                }
                // Om adressen tillhör SU (det är bara då affilieringen ska anges) och det bara finns en författare i AF/AU
                //System.Diagnostics.Debug.WriteLine("One SU address without authors, and one author in AF/AU = assume that address belongs to author");
                tmp.Add(authorsFromAfOrAu[0].PrepareAuthor(), new List<string> { address }); // Lägg till författaren från AU/AF som nyckel och adressen som enda värde i listan
                return tmp;
            }
            var authorAddressArray = authorsWithAddresses.Split(new[] { "; [" }, StringSplitOptions.None);
            foreach (var authorAddress in authorAddressArray)
            {
                if (!authorAddress.Contains("]")) // Går ej att dela upp strängen
                {
                    //System.Diagnostics.Debug.WriteLine("No right bracket = impossible to split on author list and address");
                    var note = "Felmeddelande: Adress och författare går ej att skilja åt.";
                    if (!tmp.ContainsKey(note))
                    {
                        tmp.Add(note, new List<string> { authorAddress }); // Lägg till ett felmeddelande som nyckel och adressen som enda värde i listan    
                    }
                    continue;
                }
                var address = authorAddress.Split(']')[1].Trim(new[] { ' ', '.', ',', '[', ']' }); ;
                if (Match(address))
                {
                    var authorArray = authorAddress.Split(']')[0].Split(';');
                    foreach (var author in authorArray)
                    {
                        var compauth = author.Trim();
                        var duplicateAuthorsFromAfAu = authorsFromAfOrAu.Where(auth => auth.NormalizeAuthor().Equals(compauth.NormalizeAuthor())).ToArray();
                        // Minst en motsvarande författare i AF/AU
                        if (duplicateAuthorsFromAfAu.Length > 0)
                        {
                            //System.Diagnostics.Debug.WriteLine("At least one matching author in AF/AU = choose first one");
                            if (duplicateAuthorsFromAfAu.Length > 1) // Fler än en motsvarande författare i AF/AU - rapportera fel
                            {
                                var note = "Felmeddelande: C1-författaren " + compauth + " har " + duplicateAuthorsFromAfAu.Length + " författare med samma efternamn och initial i AF/AU (" + string.Join(";", duplicateAuthorsFromAfAu) + ")";
                                if (!tmp.ContainsKey(note))
                                {
                                    tmp.Add(note, new List<string> { string.Empty });                                    
                                }
                            }
                            var authorChoice = duplicateAuthorsFromAfAu.First().PrepareAuthor(); // Välj första författaren
                            // Om författaren redan finns i tmp, lägg till adressen
                            if (tmp.ContainsKey(authorChoice) && !tmp[authorChoice].Contains(address))
                            {
                                //System.Diagnostics.Debug.WriteLine("Author exists but not address = add address");
                                tmp[authorChoice].Add(address);
                            }
                            // Lägg till författaren som nyckel och adressen som värde (i en List)    
                            else if (!tmp.ContainsKey(authorChoice))
                            {
                                //System.Diagnostics.Debug.WriteLine("Author does not exist = add author and address");
                                tmp.Add(authorChoice, new List<string> { address });
                            }
                        }
                        // Ingen motsvarande författare i AF/AU
                        else if (duplicateAuthorsFromAfAu.Length == 0)
                        {
                            //System.Diagnostics.Debug.WriteLine("No matching author in AF/AU = choose C1");
                            var note = "Felmeddelande: C1-författaren " + compauth + " har ingen matchande författare i AF/AU";
                            if (!tmp.ContainsKey(note))
                            {
                                tmp.Add(note, new List<string> { string.Empty });                                                           
                            }
                            var authorChoice = compauth.PrepareAuthor(); // Välj författaren från C1
                            if (tmp.ContainsKey(authorChoice) && !tmp[authorChoice].Contains(address))
                            {
                                //System.Diagnostics.Debug.WriteLine("Author exists but not address = add address");
                                tmp[authorChoice].Add(address);
                            }
                                // Lägg till författaren som nyckel och adressen som värde (i en List)    
                            else if (!tmp.ContainsKey(authorChoice))
                            {
                                //System.Diagnostics.Debug.WriteLine("Author does not exist = add author and address");
                                tmp.Add(authorChoice, new List<string> { address });
                            }
                        }
                    }
                }

            }
            return tmp;
        }
        // Snygga till författarsträngen inför import
        private static string PrepareAuthor(this string authorString)
        {
            var author = authorString.Trim();
            var ftmp = author.Contains(",") ? author.Split(',')[0] : string.Empty;
            var gtmp = author.Contains(",") ? author.Split(',')[1] : string.Empty;
            var fName = ftmp.Trim(new [] { ' ', ',', '[', ']' }); // Trimma efternamnet
            var gName = gtmp.Trim(new [] { ' ', ',', '[', ']' }); // Trimma förnamnet
            return string.Format("{0},{1}", fName, gName);
        }
        // Skapa namnelement med (oäkta eftersom id saknas) affiliering
        private static void CreateSuPersonElement(XElement mods, string author, IEnumerable<string> addresses, XNamespace xmlns)
        {
            var role = new XElement(xmlns + "role", new XElement(xmlns + "roleTerm", new XAttribute("type", "code"), new XAttribute("authority", "marcrelator"), "aut"));
            var fName = author.Split(',')[0];
            var gName = author.Split(',')[1];
            var namePartFamily = string.IsNullOrWhiteSpace(fName) ? null : new XElement(xmlns + "namePart", fName, new XAttribute("type", "family"));
            var namePartGiven = string.IsNullOrWhiteSpace(gName) ? null : new XElement(xmlns + "namePart", gName, new XAttribute("type", "given"));

            var nameElement = new XElement(xmlns + "name", new XAttribute("type", "personal"),
                new XAttribute("authority", "su"),
                namePartFamily, namePartGiven, role);
            foreach (var address in addresses)
            {
                nameElement.Add(new XElement(xmlns + "affiliation", address));
            }
            mods.Add(nameElement);
        }
        // Skapa namnelement utan affiliering
        private static void CreateNonSuPersonElement(XElement mods, string author, XNamespace xmlns)
        {
            var role = new XElement(xmlns + "role", new XElement(xmlns + "roleTerm", new XAttribute("type", "code"), new XAttribute("authority", "marcrelator"), "aut"));
            var ftmp = author.Contains(",") ? author.Split(',')[0] : string.Empty;
            var gtmp = author.Contains(",") ? author.Split(',')[1] : string.Empty;
            var fName = ftmp.Trim(new char[] { ' ', ',', '[', ']' }); // Normalisera efternamnet
            var gName = gtmp.Trim(new char[] { ' ', ',', '[', ']' }); // Normalisera förnamnet
            var namePartFamily = string.IsNullOrWhiteSpace(fName) ? null : new XElement(xmlns + "namePart", fName, new XAttribute("type", "family"));
            var namePartGiven = string.IsNullOrWhiteSpace(gName) ? null : new XElement(xmlns + "namePart", gName, new XAttribute("type", "given"));
            /*var namePartFamily = author.Contains(",") ? new XElement(xmlns + "namePart", author.Split(',')[0], new XAttribute("type", "family")) : null;
            var namePartGiven = author.Contains(",") ? new XElement(xmlns + "namePart", author.Split(',')[1], new XAttribute("type", "given")) : null;*/

            mods.Add(new XElement(xmlns + "name", new XAttribute("type", "personal"),
                namePartFamily, namePartGiven, role));
        }
 
        /// <summary>
        /// Lägger ev. reprint-författare med tillhörande adress i en Dictionary
        /// </summary>
        /// <param name="reprintAuthorWithAddress">WOS-formatets RP-tagg</param>
        /// <returns>En dictionary med adressen som nyckel och en array med adressens tillhörande författare som värde</returns>
        private static KeyValuePair<string,List<string>> GetReprintAddressWithAuthor(string reprintAuthorWithAddress)
        {
            
            // RP-taggen enligt följande mall: "Efternamn, Förnamn (reprint author), Organisationsadress1"
            // Dela upp på kommatecken och låt de två första orden vara efternamn och förnamn
            if (reprintAuthorWithAddress.Split(',').Length > 1)
            {
                int i = reprintAuthorWithAddress.IndexOf(',');
                i = reprintAuthorWithAddress.IndexOf(',', i + 1);
                var key = reprintAuthorWithAddress.Substring(i).Trim(new char[] { ' ', '.', ',', '[', ']' }); // Normalisera adressen    
                var tmp = reprintAuthorWithAddress.Substring(0, i).Replace("(reprint author)", string.Empty);
                var val = new List<string> { tmp };
                
                return new KeyValuePair<string, List<string>>(key, val);
            }
            else
            {
                var key = reprintAuthorWithAddress;
                var val = new List<string> { "Otypiskt reprint-format: " + reprintAuthorWithAddress };
                return new KeyValuePair<string, List<string>>(key, val);
            }
        }
        // Gör författarna från de olika wos-fälten möjliga att jämföra med varandra
        private static string NormalizeAuthor(this string authorString)
        {
            var trimmed = authorString.Trim();
            // Rensa bort allt som inte är alfanumeriskt bortsett från punkt, bindestreck, mellanslag och komma
            var atmp = new string(trimmed.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-' || c == ',' || c == '.').ToArray());
            //var regex = new Regex("[^a-zA-Z0-9 -]");
            //var atmp = authorString.Replace("[", string.Empty).Replace("]", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty).Replace(":", string.Empty).Replace(";", string.Empty);
            var aut = atmp.Split(','); // Dela upp i för- och efternamn
            if (aut.Length == 2) // Om för- och efternamn finns
            {
                var familyName = aut[0].Trim(new char[] { ' ', ',' });
                var givenName = aut[1].Trim(new char[] { ' ', ',' });
                //if (givenName.Count(x => x == '.') > 0) // Om förnamnet innehåller minst en punkt är det en initial

                return familyName + "," + givenName[0];
                //}
            }
            return "GRANSKA" + authorString;
        }
        // Helper för att kolla om ett fält existerar och har ett värde (gör koden i övrigt mer lättläst) 
        private static bool FieldExists(Dictionary<string, string> fields, string field)
        {
            // TryGetValue passar inte så bra här
            return (fields.ContainsKey(field) && !string.IsNullOrWhiteSpace(fields[field]));
        }
        
        /// <summary>
        /// Skapar en sträng i WoS-format med vissa tillägg och förändringar.
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="maxAuthorCount">Vid många upphovsmän kapas antalet upphovsmän i exporten. Detta är tröskelvärdet</param>
        /// <param name="onlyFirstLastAndLocalAuthors">Om denna är satt till true sparas bara de lokala författarna samt sista och första.</param>
        /// <returns></returns>
        public static string WoSRecordString(Dictionary<string, string> fields, int maxAuthorCount, bool onlyFirstLastAndLocalAuthors)
        {
            //Delar upp författarfältet i en array med en författare i varje.
            string[] splitAFs = fields["AF"].Split(';');

            var splitAddresses = GetSplitRPandC1(fields["RP"], fields["C1"]);

            //Hämtar en ienumerable med alla lokala författare
            IEnumerable<string[]> ownAuthorList = OwnAuthorList(splitAFs, splitAddresses).ToList();
            if (!fields.Keys.Contains("NF"))
                fields.Add("NF", "");

            string[][] authorAndAuthorAddressesList =
                splitAFs.Select(s => GetAuthorAddressFromName(s.Trim(), splitAddresses)).ToArray();

            fields["NF"] = "AuthorCount:" + authorAndAuthorAddressesList.Count() + ";" + ReConstructOwnAuthorC1ForNFField(ownAuthorList);
            var recordString = new StringBuilder("FN WoS Export Format\nVR 1.0\n");
            foreach (KeyValuePair<string, string> field in fields)
            {
                if (field.Key.Trim() != string.Empty && field.Value.Trim() != string.Empty)
                {
                    switch (field.Key)
                    {
                        case "AU":
                            break;
                        case "AF":
                            recordString.Append(field.Key + "   " + GetCorrectAFString(authorAndAuthorAddressesList, ownAuthorList.ToArray(), maxAuthorCount, onlyFirstLastAndLocalAuthors) + "\n");
                            break;
                        default:
                            recordString.Append(field.Key + " " + field.Value + "\n");
                            break;
                    }
                }
            }
            return recordString.Append("ER\n\n").ToString();
        }

        private static string[] GetSplitRPandC1(string RP, string C1)
        {
            var stringSeparators = new string[] { "; [" };

            var concatenatedAddressfields = string.Empty;
            if (!string.IsNullOrWhiteSpace(RP) && !string.IsNullOrWhiteSpace(C1))
                concatenatedAddressfields = C1 + "; [" + RP;
            else if (!string.IsNullOrWhiteSpace(C1))
                concatenatedAddressfields = C1;
            else if (!string.IsNullOrWhiteSpace(RP))
                concatenatedAddressfields = RP;

            return concatenatedAddressfields.Split(stringSeparators, StringSplitOptions.None);
        }

        private static string ReConstructOwnAuthorC1ForNFField(IEnumerable<string[]> ownAuthorList)
        {
            try
            {
                return (from string[] s in ownAuthorList
                        select "[" + s[0] + "]" + s[2]).Aggregate((a, b) => a + ";" + b);
            }
            catch (Exception ee)
            {
                return string.Empty;
            }

        }

        /// <summary>
        /// Identifierar författaradressen utifrån författarens namn.
        /// </summary>
        /// <param name="s">Författarnamnet</param>
        /// <param name="RP">REprint Address-fältet</param>
        /// <param name="C1">Fältet med författaradresser</param>
        /// <returns></returns>
        private static string[] GetAuthorAddressFromName(string s, string[] splitAddresses)
        {

            var addresses = (from string ad in splitAddresses
                             where MatchAuthorNameNew(ad, s)
                             select RemoveNamesAndBadCharactersFromAdress(ad)).Distinct().ToList();
            if (addresses.Any())
            {
                var sthlmAdresses = (from address in addresses
                                     where Match(address)
                                     select address).ToList();
                string sthlmAdressesString = string.Empty;
                if (sthlmAdresses.Any())
                    sthlmAdressesString = sthlmAdresses.Aggregate((a, b) => a + "; " + b);

                string allAddresses = addresses.Aggregate((a, b) => a + "; " + b);
                if (sthlmAdressesString != string.Empty)
                    s = "EGEN" + s;
                if (sthlmAdressesString.Contains(';'))
                    s = "DUBBELAFFILIERING" + s;
                return new string[] { s, allAddresses, sthlmAdressesString };
            }
            else
                return new string[] { s, string.Empty, string.Empty };
        }

        private static string RemoveNamesAndBadCharactersFromAdress(string adressIn)
        {
            if (adressIn.Contains(']'))
                return adressIn.Split(']')[1].Trim();
            else
                return adressIn.Trim();
        }

        public static bool MatchAuthorNameNew(string addressField, string authorName)
        {
            if (!string.IsNullOrWhiteSpace(authorName))
            {
                string[] authorNames;

                if (authorName.Contains(','))
                    authorNames = authorName.Split(',');
                else if (authorName.Contains(' '))
                    authorNames = authorName.Split(' ');
                else
                    authorNames = new[] { authorName, string.Empty };

                if (addressField.Contains(authorNames[0].Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    if (authorNames[1] != string.Empty && addressField.Contains(authorNames[0].Trim() + ", " + authorNames[1].Trim().Substring(0, 1), StringComparison.OrdinalIgnoreCase))
                        return true;
                    else return false;
                }
                else return false;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Hämtar en IEnumerable med alla SUB-författare
        /// </summary>
        /// <param name="splitAFs"></param>
        /// <param name="RP"></param>
        /// <param name="C1"></param>
        /// <returns></returns>
        private static IEnumerable<string[]> OwnAuthorList(string[] splitAFs, string[] splitAddresses)
        {
            //Hämtar alla Adresser
            var l = (from string s in splitAFs
                     select GetAuthorAddressFromName(s.Trim(), splitAddresses));

            return (from string[] authorWithAddress in l
                    where Match(authorWithAddress[1])
                    select authorWithAddress);


        }

        private static string GetCorrectAFString(string[][] authorAndAuthorAddressesList, string[][] ownAuthorList, int maxAuthors, bool onlyFirstLastAndLocalAuthors)
        {
            if (onlyFirstLastAndLocalAuthors && authorAndAuthorAddressesList.Count() > 1 && ownAuthorList.Any())
            {
                var first = ownAuthorList.Select(suAuthor => suAuthor[0]).Contains(authorAndAuthorAddressesList.First()[0]) ? string.Empty :
                    authorAndAuthorAddressesList.First()[0] + "\n   ";
                var last = ownAuthorList.Select(suAuthor => suAuthor[0]).Contains(authorAndAuthorAddressesList.Last()[0]) ? string.Empty :
                    authorAndAuthorAddressesList.Last()[0] + "\n   ";
                var suAuthors = (from string[] s in ownAuthorList
                                 select s[0]).Aggregate((a, b) => a + "\n   " + b) + "\n   ";
                return first + suAuthors + last;
            }
            if (authorAndAuthorAddressesList.Count() > maxAuthors && ownAuthorList.Any())
            {
                return (from string[] s in ownAuthorList
                        select s[0]).Aggregate((a, b) => a + "\n   " + b);
            }
            else
            {
                return (from string[] s in authorAndAuthorAddressesList
                        select s[0]).Aggregate((a, b) => a + "\n   " + b);
            }
        }
        public static bool Match(string authorAddress)
        {
            string[] regExps = { ConfigurationManager.AppSettings["OwnAuthorAffiliationRegExp"] };
            return regExps.Any(regExp => Regex.IsMatch(authorAddress.ToLower(), regExp));
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source.IndexOf(toCheck, comp) >= 0;
        }
    }
}
