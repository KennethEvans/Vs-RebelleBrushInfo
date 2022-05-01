using System.Collections.Generic;

namespace RebelleBrushInfo {
    public class CompareItem {
        public BrushParam Param1 { get; set; }
        public BrushParam Param2 { get; set; }
        public SortedDictionary<string, CompareItem> Children { get; }

        public CompareItem(BrushParam param1, BrushParam param2 = null) {
            Param1 = param1;
            Param2 = param2;
            Children = new SortedDictionary<string, CompareItem>();
        }

        /// <summary>
        /// Gets info one of the BrushParams depending on the value of which.
        /// Assumes one of them is not nulll.
        /// </summary>
        /// <param name="which">Which BrushParam to use, 1 or 2</param>
        /// <param name="prefix"></param>
        /// <param name="doChildren"></param>
        /// <param name="tab"></param>
        /// <returns></returns>
        public string getInfo(int which, string prefix = "", bool doChildren = true,
            string tab = "    ") {
            string info = "";
            if (which == 1) {
                if (Param1 == null) {
                    info += BrushParam.indented(prefix + Param2.Name
                        + ": <Not in 1>",
                        Param2.Level, tab);
                } else {
                    info += Param1.info(prefix, doChildren, tab);
                }
            } else if (which == 2) {
                if (Param2 == null) {
                    info += BrushParam.indented(prefix + Param1.Name
                        + ":  <Not in 2>",
                        Param1.Level, tab);
                } else {
                    info += Param2.info(prefix, doChildren, tab);
                }
            }
            return info;
        }
    }
}
