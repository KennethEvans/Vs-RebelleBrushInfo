using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RebelleBrushInfo {
    enum ParamType { UNKNOWN, TEXT, JOBJECT, JARRAY, IMAGE }

    class BrushParam : IComparable<BrushParam> {
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
            if (Level < 3) {
                TAB += prefix;
            }
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
                    value = "<Image>";
                    break;
                case ParamType.TEXT:
                    value = Text;
                    break;
            }
            info.Append(TAB).Append(value).Append(NL);
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
