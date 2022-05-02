using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace RebelleBrushInfo {
    public enum ParamType { UNKNOWN, TEXT, JOBJECT, JARRAY, IMAGE }

    public class BrushParam : IComparable<BrushParam> {
        public static readonly String NL = Environment.NewLine;
        public int Level { get; }
        public string Name { get; }
        public string Text { get; }
        public string Chunk { get; }
        public ParamType Type { get; }
        public List<BrushParam> Children { get; }

        public BrushParam(int level, string chunk, string name, string text) {
            Level = level;
            Chunk = chunk;
            Name = name;
            Text = text.Trim();
            Type = getType();
            if (Type == ParamType.JOBJECT) {
                Children = new List<BrushParam>();
                JObject jObject = JObject.Parse(Text);
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
                                Utils.Utils.warnMsg("Unexpected token,Type "
                                    + token.Type);
                                break;
                        }
                    }
                }
            } else if (Type == ParamType.JARRAY) {
                Children = new List<BrushParam>();
                JArray jArray = JArray.Parse(Text);
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
                                Utils.Utils.warnMsg("Unexpected token,Type "
                                    + token.Type);
                                break;
                        }
                    }
                }
            }
            // Sort the Children
            if (Children != null) {
                Children.Sort();
            }
        }

        private ParamType getType() {
            if (Chunk.Equals("PNG-tEXt")) {
                return ParamType.TEXT;
            }
            if (Chunk.Equals("PNG-zTXt")) {
                if (Text.StartsWith("{")) {
                    return ParamType.JOBJECT;
                } else if (Text.Length <= 128) {
                    // Assume a Base64 image is > this many characters
                    // Short text is the asset_id
                    return ParamType.TEXT;
                } else {
                    return ParamType.IMAGE;
                }
            }
            if (Chunk.Equals("CHILD")) {
                if (Text.StartsWith("{")) {
                    return ParamType.JOBJECT;
                } else if (Text.StartsWith("[")) {
                    return ParamType.JARRAY;
                } else {
                    return ParamType.TEXT;
                }
            }
            return ParamType.UNKNOWN;
        }

        public int Compare(BrushParam x, BrushParam y) {
            return ((BrushParam)x).Name.CompareTo(y.Name);
        }

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
            if (!this.Text.Equals(param.Text)) {
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
        /// Returns a string that is indented like BrushParam.info is. A NL is
        /// added;
        /// </summary>
        /// <param name="text"></param>
        /// <param name="level">The level to use for indentation. Get from a
        /// BrushParam</param>
        /// <param name="tab">The tab string to use, usually "    "</param>
        /// <returns>The indented string.</returns>
        public static string indented(string text, int level, string tab = "    ") {
            string info = "";
            for (int i = 1; i < level; i++) {
                info += tab;
            }
            info += text + NL;
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
        /// Generates an RTF string from the given base64 representation.
        /// </summary>
        /// <param name="base64">The base64 string.</param>
        /// <returns></returns>
        private string generateRtfImage(string base64) {
            byte[] bytes = Convert.FromBase64String(base64);
            Bitmap bm;
            using (MemoryStream ms = new MemoryStream(bytes)) {
                bm = (Bitmap)Image.FromStream(ms);
            }
            Control control = MainForm.InfoControl;
            return Utils.RTFUtils.imageRtf(control, bm);
        }

        /// <summary>
        /// Appends the text to the RichTextBox.  Moves the caret to the end
        /// of the RichTextBox's text then sets rtb.selectedRtf.
        /// </summary>
        /// <remarks>
        /// NOTE: The image is inserted wherever the caret is at the time of the call,
        /// and if any text is selected, that text is replaced.
        /// </remarks>
        /// <param name="text">The string to insert.</param>
        public static void appendRtb(string text) {
            RichTextBox rtb = (RichTextBox)MainForm.InfoControl;
            // Move carret to the end of the text
            rtb.Select(rtb.TextLength, 0);
            rtb.SelectedRtf = text;
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
        public String info(string prefix = "", bool doChildren = true,
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
                    JObject jObject = JObject.Parse(Text);
                    if (jObject == null) {
                        value = Text + " (Error parsing)";
                    } else {
                        //value = jObject.Count + " children" + NL;
                        value = "";
                    }
                    break;
                case ParamType.IMAGE:
                    value = "<Image: Hash= " + getHashForString(Text) + ">";
                    //value = generateRtfImage(Text);
                    break;
                case ParamType.TEXT:
                    value = Text;
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
