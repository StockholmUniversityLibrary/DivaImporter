using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DiVAImporterLibrary
{
    public static class MatchWoSToDiva
    {
        static Func<string, string> RH = a => a.Replace("- ", " "); //replace hyphens followed by a space with a space
        static Func<string, string> RS = a => a.Replace("-", " ");//replace remaining hyphens with a space
        
       
        /// <summary>
        /// Reads the WoS File and returns a list of Records in the form of a Dictionary with the column names as keys
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static List<Dictionary<string, string>> PrepareWosFile(StreamReader file)
        {
            var wosRecords = new List<Dictionary<string, string>>();
            string line;
            var firstLine = file.ReadLine();
            if (firstLine != null)
            {
                var fieldNames = firstLine.Split('\t');
                
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Replace("WOS:", string.Empty);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        wosRecords.Add(StandardizeWosRecord(line, fieldNames));
                    }
                }
                file.Close();
               
            }
            return wosRecords;
        }

        /// <summary>
        /// Takes a line of a tab-separated csv file and turns it into a Dictionary where the column name is the key
        /// It also standardizes the fields for matching by making some fields lower case and so on.
        /// </summary>
        /// <param name="line">Line of the file</param>
        /// <param name="fieldNames">Array of column names</param>
        /// <returns></returns>
        internal static Dictionary<string, string> StandardizeWosRecord(string line, string[] fieldNames)
        {   
            var od = new Dictionary<string, string>();
            var i = 0;
            foreach (var value in line.Replace("\"",string.Empty).Split('\t'))
            {
                if (i < fieldNames.Length)
                {
                    string newValue;
                    switch (fieldNames[i])
                    {
                        case "TI":
                            var newTI = value.ToLower(); //convert to lower case in WoS TI field
                            newTI = newTI.EndsWith(".") ? newTI.Remove(newTI.Length - 1) : newTI; //replace an ending dot with the empty string in WoS TI field
                            newTI = RH(newTI); //replace hyphens followed by a space with a space in WoS TI field
                            newTI = RS(newTI); //replace remaining hyphens with a space in WoS TI field
                            od.Add("newTI", newTI); //Add new field to keep the old one untouched
                            newValue = value;
                            break;
                        case "SO":
                            newValue = value.ToLower();
                            newValue = newValue.Replace(".", string.Empty);
                            newValue = RH(newValue);
                            newValue = RS(newValue);
                            newValue = newValue.Replace("&", "and");
                            od.Add("newSO", newValue);//Add new field to keep the old one untouched
                            break;
                        case "JI":
                            newValue = value.ToLower();
                            newValue = newValue.Replace(".", string.Empty);
                            newValue = RH(newValue);
                            newValue = RS(newValue);
                            od.Add("newJI", newValue);//Add new field to keep the old one untouched
                            break;
                        case "BP":
                            newValue = value.ToLower();
                            od.Add("newBP", newValue);//Add new field to keep the old one untouched
                            break;
                        default:
                            newValue = value;
                            break;
                    }
                    od.Add(fieldNames[i], newValue);
                    i++;
                }
            }
            od.Add("MATCHED", string.Empty);
            od.Add("FA", string.Empty);//Field for the first authors last name
            return od;
            
        }

        /// <summary>
        /// Reads the DiVA File and returns a list of Records in the form of a Dictionary with the column names as keys
        /// </summary>
        /// <param name="file"></param>
        /// <param name="isTabSeparated"></param>
        /// <returns></returns>
        public static List<Dictionary<string, string>> PrepareDivaFile(StreamReader file, bool isTabSeparated)
        {
            var divaRecords = new List<Dictionary<string, string>>();
            string line;
            var firstLine = file.ReadLine();
            if (firstLine != null)
            {
                var fieldNames = isTabSeparated ? firstLine.Split('\t') : firstLine.Split(',');
                while ((line = file.ReadLine()) != null)
                {
                    divaRecords.Add(StandardizeDivaRecord(line, fieldNames,isTabSeparated));                 
                }
            }
            file.Close();

            return divaRecords;
        }


        /// <summary>
        /// Takes a line of a tab-separated csv file and turns it into a Dictionary where the column name is the key
        /// It also standardizes the fields for matching by making some fields lower case and so on.
        /// </summary>
        /// <param name="line">Line of the file</param>
        /// <param name="fieldNames">Array of column names</param>
        /// <param name="isTabSeparated"></param>
        /// <returns></returns>
        private static Dictionary<string, string> StandardizeDivaRecord(string line, string[] fieldNames, bool isTabSeparated)
        {
            Func<string, bool, string>
               makeTabSeparated = (Line, tabSeparated) =>
                    tabSeparated ? Line.Replace("\"",string.Empty) : Line.Replace("\",\"", "\t").Replace("\"",string.Empty);
            //Dictionary for the columns and their column names
            var od = new Dictionary<string, string>();
            var i = 0;
            foreach (var value in makeTabSeparated(line                                        
                                        .Replace('å', 'a') //Nasty way to remove Scandinavian charachters...
                                        .Replace('ä', 'a')
                                        .Replace('ö', 'o')
                                        .Replace('Å', 'A')
                                        .Replace('Ä', 'A')
                                        .Replace('Ö', 'O')                                  
                                    ,isTabSeparated).Split('\t'))
                                        
            {
                if (i < fieldNames.Length)
                {
                    string a;
                    switch (fieldNames[i])
                    {
                        case "StartPage":
                            a = value;
                            od.Add("newStartPage", value.ToLower());// convert to lower case in DiVA BP field//Add new field to keep the old one untouched
                            break;
                        case "Title":
                            var newTitle = value.ToLower().TrimStart();
                            newTitle = newTitle.EndsWith(".") ? newTitle.Remove(newTitle.Length - 1) : newTitle; //replace an ending dot with the empty string in DiVA TI field
                            newTitle = RH(newTitle); //replace hyphens followed by a space with a space in DiVa TI field
                            newTitle= RS(newTitle); //replace remaining hyphens with a space in DiVA TI field
                            od.Add("newTitle", newTitle);//Add new field to keep the old one untouched
                            a = value;
                            break;
                        case "Journal":
                            var newJournal = value.ToLower(); // convert to lower case in DiVA SO field
                            newJournal = newJournal.TrimStart();
                            newJournal = newJournal.EndsWith(".") ? newJournal.Remove(newJournal.Length - 1) : newJournal; //replace dots with the empty string in DiVA SO field
                            newJournal = RH(newJournal); //replace hyphens followed by a space with a space in DiVa SO field
                            newJournal = RS(newJournal); // replace remaining hyphens with a space in DiVA SO field
                            newJournal = newJournal.Replace("&", "and"); // replace "&" with "and" in DiVA SO field
                            od.Add("newJournal", newJournal);//Add new field to keep the old one untouched
                            a = value;
                            break;
                        default:
                            a = value;
                            break;
                    }
                    od.Add(fieldNames[i], a);
                    i++;
                }
            }
            od.Add("MATCHED", string.Empty);
            od.Add("FA", string.Empty);//Field for the first authors last name
            return od;
        }

        public static IEnumerable<Tuple<Dictionary<string, string>, Dictionary<string, string>, int>> StartMatching(Dictionary<string, string>[] divaRecords, Dictionary<string, string>[] wosRecords)
        {
            var matchCriterias = new AbstractMatchCriteria[] { new Level0(), new Level1(), new Level2(), new Level3(), new Level4(), new Level5() };
            //List for the matched records
            var matchedRecords = new  List<Tuple<Dictionary<string, string>, Dictionary<string, string>, int>> ();
            foreach (var matchCriteria in matchCriterias)
            {
                foreach (var wosRecord in wosRecords)
                {
                    matchedRecords.AddRange(divaRecords.Where(a => matchCriteria.MatchCriteria(a, wosRecord) && !matchedRecords.Select(b => b.Item1["PID"]).Contains(a["PID"]) && !matchedRecords.Select(b => b.Item2["UT"]).Contains(wosRecord["UT"]))
                        .Select(a => new Tuple<Dictionary<string, string>, Dictionary<string, string>, int>(a, wosRecord, matchCriteria.Level)));
                }
         
            }
            return matchedRecords;
        }
        ///// <summary>
        ///// Här skapas kriterierna för de olika matchningsnivåerna.
        ///// </summary>
        //private static List<Func<Dictionary<string, string>, Dictionary<string, string>, bool>> MatchCriterias
        //{
        //    get
        //    {
        //        // Same ISSN?
        //        Func<Dictionary<string, string>, Dictionary<string, string>, bool>
        //        SameISSN = (divaRecord, wosRecord) =>
        //            wosRecord["SN"] == divaRecord["JournalISSN"];

        //        // Same Startpage but not emty string?
        //        Func<Dictionary<string, string>, Dictionary<string, string>, bool>
        //        SameStartPage = (divaRecord, wosRecord) =>
        //            (!String.IsNullOrEmpty(wosRecord["newBP"]) && wosRecord["newBP"] == divaRecord["newStartPage"]);

        //        //Get substring  from string start until first occurrence of a comma
        //        Func<string, string> UntilComma = a => string.IsNullOrWhiteSpace(a) ? string.Empty : a.Remove(a.IndexOf(','));


        //        // Same Volume? 
        //        Func<Dictionary<string, string>, Dictionary<string, string>, bool>
        //        SameVolume = (divaRecord, wosRecord) =>
        //            wosRecord["VL"] == divaRecord["Volume"];

        //        //Match Level 0 -  if utWoS = WoSDiVA
        //        Func<Dictionary<string, string>, Dictionary<string, string>, bool>
        //        Level0 = (diva, wos) =>
        //            wos["UT"] == diva["ISI"];

        //        //Returns the first n characters of a string provided the string is at least n characters long. 
        //        //Otherwise it returns the entire string
        //        Func<string, int, string> beg = (a, n) => a.Length > (n - 1) ? a.Substring(0, n) : a;

        //        //Match Level 1 - if issnWoS = issnDiVA AND volWoS = volDiVA AND (bpWoS = bpDiVA BUT not empty)
        //        Func<Dictionary<string, string>, Dictionary<string, string>, bool>
        //        Level1 = (divaRecord, wosRecord) =>
        //            SameISSN(divaRecord, wosRecord) &&
        //            SameVolume(divaRecord, wosRecord) &&
        //            SameStartPage(divaRecord, wosRecord);

        //        //Match Level 2 - if (soWoS15 = soDiVA15 OR jiWoS12 = soDiVA12 OR tiWoS20 = tiDiVA20) AND (volWoS = volDiVA 
        //        //AND (bpWoS = bpDiVA BUT not empty))
        //        Func<Dictionary<string, string>, Dictionary<string, string>, bool>
        //        Level2 = (d, w) =>
        //            (beg(w["newSO"], 15) == beg(d["newJournal"], 15) || beg(w["newJI"], 12) == beg(d["newJournal"], 12) || beg(w["newTI"], 20) == beg(d["newTitle"], 20)) &&
        //            (SameVolume(d, w) && SameStartPage(d, w));

        //        //Match Level 3 - if (fAUWoS = fAUDiVA) AND (issnWoS = issnDiVA OR soWoS1 = soDiVA1) AND (volWoS = volDiVA OR (bpWoS = bpDiVA BUT not empty))
        //        Func<Dictionary<string, string>, Dictionary<string, string>, bool>
        //        Level3 = (d, w) =>
        //           UntilComma(w["AU"]) == UntilComma(d["Name"]) &&
        //            (SameISSN(d, w) || beg(d["newJournal"], 1) == beg(w["newSO"], 1)) &&
        //            (SameVolume(d, w) || SameStartPage(d, w));

        //        //Match Level 4 - if (fAUWoS = fAUDiVA AND tiWoS25 = tiDiVA25) OR (fAUWoS = fAUDiVA AND tiWoS10 = tiDiVA10 AND (bpWoS = bpDiVA BUT not empty))
        //        Func<Dictionary<string, string>, Dictionary<string, string>, bool>
        //         Level4 = (d, w) =>
        //              (UntilComma(w["AU"]) == UntilComma(d["Name"]) && beg(w["newTI"], 25) == beg(d["newTitle"], 25)) ||
        //              (UntilComma(w["AU"]) == UntilComma(d["Name"]) && beg(w["newTI"], 10) == beg(d["newTitle"], 10) && SameStartPage(d, w));

        //        //Match Level 5 - if (tiWoS30 = tiDiVA30) OR (tiWoS10 = tiDiVA10 AND (bpWoS = bpDiVA BUT not empty)) OR
        //        // (fAUWoS = fAUDiVA AND tiWoS10 = tiDiVA10) OR (fAUWoS = fAUDiVA AND (bpWoS = bpDiVA BUT not empty))
        //        Func<Dictionary<string, string>, Dictionary<string, string>, bool>
        //        Level5 = (d, w) =>
        //             beg(w["newTI"], 30) == beg(d["newTitle"], 30) ||
        //             (beg(w["newTI"], 10) == beg(d["newTitle"], 10) && SameStartPage(d, w)) ||
        //             (UntilComma(w["AU"]) == UntilComma(d["Name"]) && beg(w["newTI"], 10) == beg(d["newTitle"], 10)) ||
        //              (UntilComma(w["AU"]) == UntilComma(d["Name"]) && SameStartPage(d, w));



        //        //Put the match functors in a List
        //        List<Func<Dictionary<string, string>, Dictionary<string, string>, bool>> matchCriterias =
        //            new List<Func<Dictionary<string, string>, Dictionary<string, string>, bool>>() { Level0, Level1, Level2, Level3, Level4, Level5 };
        //        return matchCriterias;
        //    }
        //}

        ///// <summary>
        ///// Outputs the Match results to a text file containing ids of matched records and their match level.
        ///// The file also contains the full unmatched records from both DiVA and WoS
        ///// </summary>
        ///// <param name="matchResult">The Tuples of the matched records</param>
        ///// <param name="unmatchedWos">IEnumerable of unmatched WoS records</param>
        ///// <param name="unmatchedDivas">IEnumerable of unmatched DiVA records</param>
        ///// <param name="filePath">path to save the files</param>
        ///// <param name="wosRecordCount">Number of WoS Records in total that went into the matching</param>
        ///// <param name="divaRecordCount">Number of DiVARecords in total that went into the matching</param>
        //internal static void SaveMatchResultToFile(Tuple<Dictionary<string, string>, Dictionary<string, string>, int>[] matchResult, Dictionary<string, string>[] unmatchedWos, Dictionary<string, string>[] unmatchedDivas, string filePath, int wosRecordCount, int divaRecordCount)
        //{
        //    var s = new StringBuilder(); 
        //    var fileName = filePath + "\\MatchingResult_" + DateTime.Now.ToShortDateString() + ".txt";
        //    TextWriter w = new StreamWriter(fileName);
        //    s.Append("Number of WoS (DiVA) records: "+wosRecordCount+" ("+ divaRecordCount+")\n\n");
        //    if (matchResult.Any())
        //    {
        //        s.Append("MATCHINGS\n\nWoS_ID \t DiVA_ID \t Match_level\n\n");
        //        s.Append(matchResult
        //            .OrderBy(a => a.Item3)
        //            .Select(a => a.Item2["UT"] + " \t " + a.Item1["PID"] + " \t " + a.Item3.ToString(CultureInfo.InvariantCulture) + "\n")
        //            .Aggregate((a, b) => a + b) + "\n");
        //        s.Append("\n# matchings: " + matchResult.Count() + "\n\n");
        //    }
        //    else
        //    {
        //        s.Append("NO MATCHINGS FOUND\n\n");
        //    }
        //    s.Append("UNMATCHED, WoS\n\n");
        //    s.Append(unmatchedWos.Select(a => string.Join("\t", a.Where(b => !b.Key.StartsWith("new") && b.Key != "FA").Select(c => c.Value)) + "\n").Aggregate((a, b) => a + b) + "\n");
        //    s.Append("# unmatched WoS records: " + unmatchedWos.Count() + "\n\n");

        //    s.Append("UNMATCHED, DiVA\n\n");
        //    s.Append(unmatchedDivas.Select(a => string.Join("\t", a.Where(b => !b.Key.StartsWith("new") && b.Key != "FA").Select(c=>c.Value)) + "\n").Aggregate((a, b) => a + b) + "\n");
        //    s.Append("# unmatched DiVA records: "+ unmatchedDivas.Count());

        //    s.Append("\nEF");
        //    w.Write(s);
        //    w.Flush();
        //    w.Close();
        //}
    }
}
