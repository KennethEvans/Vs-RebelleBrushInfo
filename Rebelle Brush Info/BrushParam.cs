using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RebelleBrushInfo {
    enum ParamType { UNKNOWN, TEXT, JOBJECT, JARRAY, IMAGE }

    class BrushParam : IComparer {
        public static readonly String NL = Environment.NewLine;
        public int Level { get; }
        public string Name { get; }
        public string Text { get; }
        public string Chunk { get; }
        public ParamType Type { get; }
        public List<BrushParam> Children { get; }

        public BrushParam(int level, string chunk, string name, string text) {
#if false
            text = @"{""paint_mix_curve_2"": {
        ""fromParams"": false,
        ""function"": 2,
        ""maximum"": 1,
        ""minimum"": 0,
        ""multiplier"": 1,
        ""outputMax"": 1,
        ""outputMin"": 0,
        ""points"": [
            {
                    ""x"": 0.5,
                ""y"": 1
            }
        ]
    }}";
#endif
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
                    foreach (JToken token in jArray.Children()) {
                        switch (token.Type) {
                            case JTokenType.Object:
                                JObject jObject = token as JObject;
                                BrushParam param1 = new BrushParam(level + 1,
                                    "CHILD", "<Unnamed>", jObject.ToString());
                                Children.Add(param1);
                                break;
                            case JTokenType.Array:
                                JArray jArray1 = token as JArray;
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
        }

        private ParamType getType() {
            if (Chunk.Equals("PNG-tEXt")) {
                return ParamType.TEXT;
            }
            if (Chunk.Equals("PNG-zTXt")) {
                if (Text.StartsWith("{")) {
                    return ParamType.JOBJECT;
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

        public int Compare(object x, object y) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns if the given BrushParam is equal to this one.
        /// </summary>
        /// <param Name="param"></param>
        /// <returns></returns>
        public bool equals(BrushParam param) {
            if (!this.Name.Equals(param.Name)) {
                return false;
            }
            if (!this.Name.Equals(param.Name)) {
                return false;
            }
            if (!this.Name.Equals(param.Name)) {
                return false;
            }
            if (!this.Name.Equals(param.Name)) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Returns an information string for this RebelleBrushParam. This consists
        /// of the Name and the result of getValueByString().
        /// </summary>
        /// <param name="tab">Prefix for each line, typically "  " or similar.</param>
        /// <returns></returns>
        public String info1(string tab) {
            StringBuilder info;
            info = new StringBuilder();
            info.Append(Type).Append(" ");
            info.Append(Name).Append(": ");
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
                        value = jObject.Count + " children" + NL;
                        int nChildren = 0;
                        foreach (JToken token in jObject.Children()) {
                            value += "    " + nChildren++ + " " + token.Type
                                + " " + token + NL;
                        }
                    }
                    break;
                case ParamType.IMAGE:
                    value = "<Image>";
                    break;
                case ParamType.TEXT:
                    value = Text;
                    break;
            }
            info.Append(value).Append(NL);
            return info.ToString();
        }

        /// <summary>
        /// Returns an information string for this RebelleBrushParam. This consists
        /// of the Name and the result of getValueByString().
        /// </summary>
        /// <returns></returns>
        public String info() {
            string TAB = "";
            for (int i = 1; i < Level; i++) {
                TAB += "    ";
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
            if (Children != null) {
                //int nChildren = 0;
                foreach (BrushParam brushParam in Children) {
                    //info.Append(TAB).Append("Child ").Append(nChildren++).Append(NL);
                    info.Append(TAB).Append(brushParam.info());
                    //if (brushParam.Children != null) {
                    //    int nChildren1 = 0;
                    //    foreach (BrushParam brushParam1 in brushParam.Children) {
                    //        info.Append(TAB).Append("SubChild ").Append(nChildren1++).Append(NL);
                    //        info.Append(TAB).Append(brushParam1.info());
                    //    }
                    //}
                }
            }
            return info.ToString();
        }

    }
}
