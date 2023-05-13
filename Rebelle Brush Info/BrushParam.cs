//#define USE_HASH_FOR_IMAGE

using KEUtils.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace RebelleBrushInfo {
    public enum ParamType { UNKNOWN, TEXT, JOBJECT, JARRAY, IMAGE, CURVE }

    public class BrushParam : IComparable<BrushParam> {
        public static readonly String NL = Environment.NewLine;
        //public static readonly char Delim = '\u00d0';
        public static readonly char Delim = '\0';
        public int Level { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string Chunk { get; set; }
        public ParamType Type { get; set; }
        public List<BrushParam> Children { get; set; }

        public BrushParam() {

        }

        public BrushParam(int level, string chunk, string name, string value) {
            Level = level;
            Chunk = chunk;
            Name = name;
            Value = value.Trim();
            Type = getType();
            if (Type == ParamType.JOBJECT) {
                Children = new List<BrushParam>();
                JObject jObject = JObject.Parse(Value);
                if (jObject == null) {
                    // TO DO
                } else {
                    foreach (JToken token in jObject.Children()) {
                        switch (token.Type) {
                            case JTokenType.Property:
                                JProperty jProperty = token as JProperty;
                                BrushParam param = new BrushParam(level + 1,
                                    "CHILD",
                                    jProperty.Name, jProperty.Value.ToString());
                                Children.Add(param);
                                break;
                            default:
                                Utils.warnMsg("Unexpected token,Type "
                                    + token.Type);
                                break;
                        }
                    }
                }
            } else if (Type == ParamType.JARRAY) {
                Children = new List<BrushParam>();
                JArray jArray = JArray.Parse(Value);
                if (jArray == null) {
                    // TO DO
                } else {
                    int nArray = 0;
                    foreach (JToken token in jArray.Children()) {
                        switch (token.Type) {
                            case JTokenType.Object:
                                JObject jObject = token as JObject;
                                BrushParam param1 = new BrushParam(level + 1,
                                    "CHILD", $"[{++nArray}]", jObject.ToString());
                                Children.Add(param1);
                                break;
                            case JTokenType.Property:
                                JProperty jProperty = token as JProperty;
                                BrushParam param = new BrushParam(level + 1,
                                    "CHILD",
                                    jProperty.Name, jProperty.Value.ToString());
                                Children.Add(param);
                                break;
                            default:
                                Utils.warnMsg("Unexpected token,Type "
                                    + token.Type);
                                break;
                        }
                    }
                }
            }
            // Sort the Children
            if (Children != null) {
                Children.Sort();
            } else if (Type == ParamType.CURVE) {
                // Add spacing in front in front of the multiline text
                string newValue = "";
                //using (StringReader sr = new StringReader(Value)) {
                //    string line;
                //    while ((line = sr.ReadLine()) != null) {
                //        newValue += "            " + line + NL;
                //    }
                //}
                newValue = RebelleCurve.formatCurve("    ", Value);
                // Remove last NL
                if (newValue.EndsWith(NL)) {
                    newValue = newValue.Substring(0, newValue.Length - NL.Length);
                }

                // Create an image
                RebelleCurve rebelleCurve = RebelleCurve.getRebelleCurve(Value);
                Bitmap bm = RebelleCurve.getCurveImage(rebelleCurve);
                string base64 = MainForm.convertImageToBase64(bm);

                Value = NL + newValue + Delim + base64 + Delim;
            }
        }

        private ParamType getType() {
            if (Chunk.Equals("PNG-tEXt")) {
                return ParamType.TEXT;
            }
            if (Chunk.Equals("PNG-zTXt")) {
                if (Value.StartsWith("{")) {
                    return ParamType.JOBJECT;
                } else if (Value.Length <= 128) {
                    // Assume a Base64 image is > this many characters
                    // Short value is the asset_id
                    return ParamType.TEXT;
                } else {
                    return ParamType.IMAGE;
                }
            }
            if (Chunk.Equals("CHILD")) {
                if (Value.StartsWith("{")) {
                    if (Name.Contains("_curve")) {
                        return ParamType.CURVE;
                    }
                    return ParamType.JOBJECT;
                } else if (Value.StartsWith("[")) {
                    return ParamType.JARRAY;
                } else {
                    return ParamType.TEXT;
                }
            }
            return ParamType.UNKNOWN;
        }

        /// <summary>
        /// Compare compares the names of the two BrushParams.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int Compare(BrushParam x, BrushParam y) {
            return ((BrushParam)x).Name.CompareTo(y.Name);
        }

        /// <summary>
        /// CompareTo compares the names of this and other.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(BrushParam other) {
            return (this.Name.CompareTo(((BrushParam)other).Name));
        }

        /// <summary>
        /// Returns if the given BrushParam is equal to this one including
        /// children.
        /// </summary>
        /// <param name="param">The BrushParm to compare with.</param>
        /// <returns></returns>
        public bool equals(BrushParam param) {
            if (!this.Name.Equals(param.Name)) {
                return false;
            }
            if (!this.Value.Equals(param.Value)) {
                return false;
            }
            if (this.Type != param.Type) {
                return false;
            }
            if (this.Level != param.Level) {
                return false;
            }
            // Check children
            if (this.Children == null && param.Children == null) {
                // No children, done
                return true;
            }
            if (this.Children == null && param.Children != null) {
                return false;
            }
            if (this.Children != null && param.Children == null) {
                return false;
            }
            // Both have children
            if (this.Children.Count != param.Children.Count) {
                return false;
            }
            // Both have the same number of children
            for (int i = 0; i < this.Children.Count; i++) {
                if (!this.Children[i].equals(param.Children[i])) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Creates a clone of this BrushParam
        /// </summary>
        /// <returns></returns>
        public BrushParam clone() {
            BrushParam param = new BrushParam();
            param.Level = this.Level;
            param.Name = this.Name;
            param.Value = this.Value;
            param.Chunk = this.Chunk;
            param.Type = this.Type;
            param.Type = this.Type;
            if (this.Children != null) {
                param.Children = new List<BrushParam>();
                foreach (BrushParam param0 in this.Children) {
                    param.Children.Add(param0.clone());
                }
            }
            return param;
        }

        /// <summary>
        /// Returns a string that is indented like BrushParam.info is. A NL is
        /// added;
        /// </summary>
        /// <param name="value"></param>
        /// <param name="level">The level to use for indentation. Get from a
        /// BrushParam</param>
        /// <param name="tab">The tab string to use, usually "    "</param>
        /// <returns>The indented string.</returns>
        public static string indented(string value, int level, string tab = "    ") {
            string info = "";
            for (int i = 1; i < level; i++) {
                info += tab;
            }
            info += value + NL;
            return info;
        }

        /// <summary>
        /// Recursively adds this BrushParam and its children to the given dictionary.
        /// </summary>
        /// <param name="which">1 or 2, depending on the position in the BrushItem</param>
        /// <param name="param">The BrushParam to add.</param>
        /// <param name="items">The discionary to add it to.</param>
        public static void addToDictionary(int which, BrushParam param,
            SortedDictionary<string, CompareItem> items) {
            CompareItem item = null;
            bool exists;
            if (param.Name.Equals("brush_rotation_mode")) {
                string temp = param.Name;
            }
            if (param.Name.Equals("Parameters")) {
                string temp = param.Name;
            }
            if (items.ContainsKey(param.Name)) {
                // DEBUG
                // Key exists in dictionary
                if (which == 1) {
                    exists = items.TryGetValue(param.Name, out item);
                    if (exists) {
                        item.Param1 = param;
                    } else {
                        item = new CompareItem(param, null);
                        items.Add(param.Name, item);
                    }
                    // Add children
                    if (param.Children != null) {
                        SortedDictionary<string, CompareItem> childDictionary =
                            item.Children;
                        foreach (BrushParam child in param.Children) {
                            if (param.Name.Equals("brush_rotation_mode")) {
                                string temp = param.Name;
                            }
                            addToDictionary(1, child, childDictionary);
                        }
                    }
                } else if (which == 2) {
                    exists = items.TryGetValue(param.Name, out item);
                    if (exists) {
                        item.Param2 = param;
                    } else {
                        item = new CompareItem(null, param);
                        items.Add(param.Name, item);
                    }
                }
            } else {
                // Key does not exist in dictionary
                if (which == 1) {
                    item = new CompareItem(param);
                    items.Add(param.Name, item);
                } else if (which == 2) {
                    item = new CompareItem(null, param);
                    items.Add(param.Name, item);
                }
            }
            // Add children
            if (param.Children != null) {
                SortedDictionary<string, CompareItem> childDictionary =
                    item.Children;
                foreach (BrushParam child in param.Children) {
                    addToDictionary(which, child, childDictionary);
                }
            }
        }

        /// <summary>
        /// Gets a hash code for a string that is reasonably unique. 
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private string getHashForString(string str) {
            string hash;
            using (System.Security.Cryptography.MD5 md5 =
                System.Security.Cryptography.MD5.Create()) {
                hash = BitConverter.ToString(
                  md5.ComputeHash(Encoding.UTF8.GetBytes(str))
                ).Replace("-", String.Empty);
            }
            return hash;
        }

        /// <summary>
        /// Returns an information string for this RebelleBrushParam. This consists
        /// of the Name and the result of getValueByString().
        /// </summary>
        /// <returns></returns>
        public string info(string prefix = "", bool doChildren = true,
                    string tab = "   ") {
            string TAB = "";
            for (int i = 1; i < Level; i++) {
                TAB += tab;
            }
            TAB += prefix;
            StringBuilder info;
            info = new StringBuilder();
            //info.Append(TAB).Append(Type).Append(" ");
            //info.Append(TAB).Append("Level ").Append(Level).Append(" ");
            info.Append(TAB).Append(Name).Append(": ");
            string value = "";
            switch (Type) {
                case ParamType.UNKNOWN:
                    value = "Unknown";
                    break;
                case ParamType.JOBJECT:
                    JObject jObject = JObject.Parse(Value);
                    if (jObject == null) {
                        value = Value + " (Error parsing)";
                    } else {
                        //value = jObject.Count + " children" + NL;
                        value = "";
                    }
                    break;
                case ParamType.IMAGE:
#if USE_HASH_FOR_IMAGE
                    value = "<Image: Hash= " + getHashForString(Value) + ">";
#else
                    value = Delim + Value + Delim;
#endif
                    break;
                case ParamType.TEXT:
                    value = Value;
                    break;
                case ParamType.CURVE:
                    value = Value;
                    break;
            }
            info.Append(value).Append(NL);
            if (!doChildren) return info.ToString();
            if (Children != null) {
                //int nChildren = 0;
                foreach (BrushParam brushParam in Children) {
                    //info.Append(TAB).Append("Child ").Append(nChildren++).Append(NL);
                    info.Append(TAB).Append(brushParam.info(prefix, doChildren));
                }
            }
            return info.ToString();
        }
    }
}
