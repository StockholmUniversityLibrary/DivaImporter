using System;
using System.Collections.Generic;

namespace DiVAImporterLibrary
{
    public abstract class AbstractMatchCriteria
    {
        /// <summary>
        /// Get substring  from string start until first occurrence of a comma
        /// </summary>
        internal static Func<string, string> UntilComma = a => string.IsNullOrWhiteSpace(a) ? string.Empty : a.Remove(a.IndexOf(','));

        /// <summary>
        /// Same ISSN?
        /// </summary>
        internal static readonly Func<Dictionary<string, string>, Dictionary<string, string>, bool>
            SameIssn = (divaRecord, wosRecord) =>
                wosRecord["SN"] == divaRecord["JournalISSN"];

        /// <summary>
        /// Same Startpage but not emty string?
        /// </summary>
        internal static readonly Func<Dictionary<string, string>, Dictionary<string, string>, bool>
           SameStartPage = (divaRecord, wosRecord) =>
               (!String.IsNullOrEmpty(wosRecord["newBP"]) && wosRecord["newBP"] == divaRecord["newStartPage"]);

        /// <summary>
        /// Same volume?
        /// </summary>
        internal static readonly Func<Dictionary<string, string>, Dictionary<string, string>, bool>
            SameVolume = (divaRecord, wosRecord) =>
                wosRecord["VL"] == divaRecord["Volume"];
        /// <summary>
        ///  Returns the first n characters of a string provided the string is at least n characters long. Otherwise it returns the entire string
        /// </summary>
        internal static readonly Func<string, int, string> Beg = (a, n) => a.Length > (n - 1) ? a.Substring(0, n) : a;

        public abstract int Level { get; }
        public abstract string Description { get; }
        public abstract Func<Dictionary<string, string>, Dictionary<string, string>, bool> MatchCriteria { get; }


    }
    public class Level0 : AbstractMatchCriteria
    {

        public override int Level
        {
            get { return 0; }
        }
        /// <summary>
        /// Match criteria Level 0 -  if utWoS = isiDiVA
        /// </summary>
        public override Func<Dictionary<string, string>, Dictionary<string, string>, bool> MatchCriteria
        {
            get {return (diva, wos) => wos["UT"] == diva["ISI"]; }
        }
        public override string Description
        {
            get { return "utWoS = isiDiVA"; }
        }
    }

    public class Level1 : AbstractMatchCriteria
    {

        public override int Level
        {
            get { return 1; }
        }
        /// <summary>
        /// Match Level 1 - if issnWoS = issnDiVA AND volWoS = volDiVA AND (bpWoS = bpDiVA BUT not empty)
        /// </summary>
        public override Func<Dictionary<string, string>, Dictionary<string, string>, bool> MatchCriteria
        {
            get
            {
                return (divaRecord, wosRecord) =>
                    SameIssn(divaRecord, wosRecord) &&
                    SameVolume(divaRecord, wosRecord) &&
                    SameStartPage(divaRecord, wosRecord);
            }
        }
        public override string Description
        {
            get { return "issnWoS = issnDiVA AND volWoS = volDiVA AND (bpWoS = bpDiVA BUT not empty)"; }
        }
    }

    public class Level2 : AbstractMatchCriteria
    {

        public override int Level
        {
            get { return 2; }
        }
        /// <summary>
        /// Match Level 2 - if (soWoS15 = soDiVA15 OR jiWoS12 = soDiVA12 OR tiWoS20 = tiDiVA20) AND (volWoS = volDiVA AND (bpWoS = bpDiVA BUT not empty))
        /// </summary>
        public override Func<Dictionary<string, string>, Dictionary<string, string>, bool> MatchCriteria
        {
            get
            {
                return (d, w) =>
                (Beg(w["newSO"], 15) == Beg(d["newJournal"], 15) || Beg(w["newJI"], 12) == Beg(d["newJournal"], 12) || Beg(w["newTI"], 20) == Beg(d["newTitle"], 20)) &&
                (SameVolume(d, w) && SameStartPage(d, w));
            }
        }
        public override string Description
        {
            get { return "(soWoS15 = soDiVA15 OR jiWoS12 = soDiVA12 OR tiWoS20 = tiDiVA20) AND (volWoS = volDiVA AND (bpWoS = bpDiVA BUT not empty))"; }
        }
    }
    public class Level3 : AbstractMatchCriteria
    {

        public override int Level
        {
            get { return 3; }
        }
        /// <summary>
        /// Match Level 3 - if (fAUWoS = fAUDiVA) AND (issnWoS = issnDiVA OR soWoS1 = soDiVA1) AND (volWoS = volDiVA OR (bpWoS = bpDiVA BUT not empty))
        /// </summary>
        public override Func<Dictionary<string, string>, Dictionary<string, string>, bool> MatchCriteria
        {
            get
            {
                return (d, w) =>
               UntilComma(w["AU"]) == UntilComma(d["Name"]) &&
                (SameIssn(d, w) || Beg(d["newJournal"], 1) == Beg(w["newSO"], 1)) &&
                (SameVolume(d, w) || SameStartPage(d, w));
            }
        }
        public override string Description
        {
            get { return "(fAUWoS = fAUDiVA) AND (issnWoS = issnDiVA OR soWoS1 = soDiVA1) AND (volWoS = volDiVA OR (bpWoS = bpDiVA BUT not empty))"; }
        }
    }
    public class Level4 : AbstractMatchCriteria
    {
        public override int Level
        {
            get { return 4; }
        }
        /// <summary>
        /// Match Level 4 - if (fAUWoS = fAUDiVA AND tiWoS25 = tiDiVA25) OR (fAUWoS = fAUDiVA AND tiWoS10 = tiDiVA10 AND (bpWoS = bpDiVA BUT not empty))
        /// </summary>
        public override Func<Dictionary<string, string>, Dictionary<string, string>, bool> MatchCriteria
        {
            get
            {
                return (d, w) =>
                  (UntilComma(w["AU"]) == UntilComma(d["Name"]) && Beg(w["newTI"], 25) == Beg(d["newTitle"], 25)) ||
                  (UntilComma(w["AU"]) == UntilComma(d["Name"]) && Beg(w["newTI"], 10) == Beg(d["newTitle"], 10) && SameStartPage(d, w));
            }
        }
        public override string Description
        {
            get { return "(fAUWoS = fAUDiVA AND tiWoS25 = tiDiVA25) OR (fAUWoS = fAUDiVA AND tiWoS10 = tiDiVA10 AND (bpWoS = bpDiVA BUT not empty))"; }
        }
    }
    public class Level5 : AbstractMatchCriteria
    {
        public override int Level
        {
            get { return 5; }
        }
        /// <summary>
        /// Match Level 5 - if (tiWoS30 = tiDiVA30) OR (tiWoS10 = tiDiVA10 AND (bpWoS = bpDiVA BUT not empty)) OR (fAUWoS = fAUDiVA AND tiWoS10 = tiDiVA10) OR (fAUWoS = fAUDiVA AND (bpWoS = bpDiVA BUT not empty))
        /// </summary>
        public override Func<Dictionary<string, string>, Dictionary<string, string>, bool> MatchCriteria
        {
            get
            {
                return (d, w) =>
                 Beg(w["newTI"], 30) == Beg(d["newTitle"], 30) ||
                 (Beg(w["newTI"], 10) == Beg(d["newTitle"], 10) && SameStartPage(d, w)) ||
                 (UntilComma(w["AU"]) == UntilComma(d["Name"]) && Beg(w["newTI"], 10) == Beg(d["newTitle"], 10)) ||
                  (UntilComma(w["AU"]) == UntilComma(d["Name"]) && SameStartPage(d, w));
            }
        }
        public override string Description
        {
            get { return "(tiWoS30 = tiDiVA30) OR (tiWoS10 = tiDiVA10 AND (bpWoS = bpDiVA BUT not empty)) OR (fAUWoS = fAUDiVA AND tiWoS10 = tiDiVA10) OR (fAUWoS = fAUDiVA AND (bpWoS = bpDiVA BUT not empty))"; }
        }
    }
}
