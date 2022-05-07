//#define debugging
//#define replaceDoctype
//#define TEST

using About;
using MetadataExtractor;
using RebelleUtils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace RebelleBrushInfo {

    public partial class MainForm : Form {
        enum FileType { Brush1, Brush2 };
        public static readonly int PROCESS_TIMEOUT = 5000; // ms
        public static readonly String NL = Environment.NewLine;
        private static readonly int imageSize = 256;
        private static ScrolledHTMLDialog overviewDlg;
        //private static ScrolledRichTextDialog textDlg;
        private static FindDialog findDlg;

        private List<BrushParam> paramsList1 = new List<BrushParam>();
        private List<BrushParam> paramsList2 = new List<BrushParam>();
        private string info1;
        private string info2;
        private string header1;
        private string header2;
        public MainForm() {
            InitializeComponent();

            textBoxBrush1.Text = Properties.Settings.Default.DatabaseName1;
            textBoxCrush2.Text = Properties.Settings.Default.DatabaseName2;
        }

        /// <summary>
        /// Process a brush.
        /// </summary>
        /// <param name="fileType">Determines if databse 1 or database 2</param>
        /// <param name="print">Whether to write to textBoxInfo.</param>
        private void processBrush(FileType fileType, bool print) {
            int nBrush = 1;
            TextBox textBoxBrush = null;
            List<BrushParam> paramsList = null;
            BrushParam param = null;
            string info = "";
            string header = "";
            switch (fileType) {
                case FileType.Brush1:
                    nBrush = 1;
                    textBoxBrush = textBoxBrush1;
                    break;
                case FileType.Brush2:
                    nBrush = 2;
                    textBoxBrush = textBoxCrush2;
                    break;
                default:
                    Utils.Utils.errMsg("Invalid fileType ("
                        + fileType + ") for processBrush");
                    return;
            }
            textBoxInfo.Clear();
            paramsList = new List<BrushParam>();
            String fileName = textBoxBrush.Text;
            if (fileName == null || fileName.Length == 0) {
                registerOutput(fileType, info, header, paramsList);
                Utils.Utils.errMsg("Brush " + nBrush + " is not defined");
                return;
            }
            if (!File.Exists(fileName)) {
                registerOutput(fileType, info, header, paramsList);
                Utils.Utils.errMsg(fileName + " does not exist");
                return;
            }
            // Get the selected brush fileName
            string brushName = Path.GetFileNameWithoutExtension(fileName);
            if (brushName == null | brushName.Length == 0) {
                registerOutput(fileType, info, header, paramsList);
                Utils.Utils.errMsg("Brush not specified");
                return;
            }
            info += brushName + NL;

            // Get the metadata
            IReadOnlyList<MetadataExtractor.Directory> directories =
                ImageMetadataReader.ReadMetadata(fileName);
            info += getFileInfo(directories) + NL;
            // Output above is the header
            header = info;
            info = "";

#if TEST
            // This is the simple output just using the metadata
            foreach (MetadataExtractor.Directory directory in directories) {
                info += directory.Name + NL;
                foreach (Tag tag in directory.Tags) {
                    info += $"    {tag.Name} = {tag.Description}" + NL;
                }
                if (directory.HasError) {
                    foreach (var error in directory.Errors)
                        info += $"ERROR: {error}";
                }
            }
#else
            // Make a List<MetadataDirectory> from the Dictionary
            // So it can be sorted
            List<Metadata> metadataList = new List<Metadata>();
            foreach (MetadataExtractor.Directory directory in directories) {
                // Don't do the non-Rebelle ones
                if (!directory.Name.Equals("PNG-tEXt") &&
                    !directory.Name.Equals("PNG-zTXt")) {
                    continue;
                }
                int len, index;
                string name, text;
                foreach (Tag tag in directory.Tags) {
                    len = tag.Description.Length;
                    index = tag.Description.IndexOf(":");
                    name = "";
                    text = "";
                    if (index > -1) {
                        if (directory.Name.Equals("PNG-tEXt")) {
                            name = tag.Description.Substring(0, index);
                            text = tag.Description.Substring(index + 1);
                        } else if (directory.Name.Equals("PNG-zTXt")) {
                            name = tag.Description.Substring(0, index);
                            text = tag.Description.Substring(index + 1);
                        }
                    }
                    metadataList.Add(new Metadata(directory.Name,
                        name, text));
                }
            }
            metadataList.Sort((x, y) => x.Name.CompareTo(y.Name));

            // Loop over it
            foreach (Metadata metadata in metadataList) {
                //info += directory.Name + NL;
                param = new BrushParam(1, metadata.DirectoryName,
                    metadata.Name, metadata.Text);
                paramsList.Add(param);
                info += $"{param.info()}" + NL;
            }
            paramsList.Sort();
#endif
            registerOutput(fileType, info, header, paramsList);
        }

        /// <summary>
        /// Compares the two files and displays the output.
        /// </summary>
        /// <param name="all">Whether to show all items or just differences.</param>
        private void compare(Boolean all = false) {
            // Process brush 1
            processBrush(FileType.Brush1, false);
            if (paramsList1.Count == 0) {
                Utils.Utils.errMsg("Did not get params for Brush 1");
                return;
            }
            // Process brush 2
            processBrush(FileType.Brush2, false);
            if (paramsList2.Count == 0) {
                Utils.Utils.errMsg("Did not get params for Brush 2");
                return;
            }

            // Write heading to textBoxInfo
            textBoxInfo.Text = "1: ";
            printHeading(FileType.Brush1);
            appendInfo("2: ");
            printHeading(FileType.Brush2);

            // Make a SortedDictionary holding all the items from both brushes.
            SortedDictionary<string, CompareItem> items =
                new SortedDictionary<string, CompareItem>();
            foreach (BrushParam param in paramsList1) {
                BrushParam.addToDictionary(1, param, items);
            }
            foreach (BrushParam param in paramsList2) {
                BrushParam.addToDictionary(2, param, items);
            }
            string info = getItemInfo(items, all);

            // Parse the info to get the images so they can be inserted
            appendInfoWithImages(info);
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
                bm = new Bitmap(bm, new Size(imageSize, imageSize));
            }
            return Utils.RTFUtils.imageRtf(textBoxInfo, bm);
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
        public static void appendRtb(RichTextBox rtb, string text) {
            // Move carret to the end of the text
            rtb.Select(rtb.TextLength, 0);
            rtb.SelectedRtf = text;
        }

        /// <summary>
        /// Get the info for the given dictionary and its children recursively.
        /// </summary>
        /// <param name="items">The dictionary.</param>
        /// <returns>The information.</returns>
        private string getItemInfo(SortedDictionary<string, CompareItem> items,
            bool all) {
            string info = "";
            string info1, info2, indxStr1 = "  1 ", indxStr2 = "  2 ";
            int pos1, pos2;
            int level;
            ParamType type;
            CompareItem item;
            SortedDictionary<string, CompareItem>.KeyCollection keys = items.Keys;
            foreach (string key in keys) {
                bool res = items.TryGetValue(key, out item);
                if (res) {
                    level = (item.Param1 != null) ? item.Param1.Level : item.Param2.Level;
                    type = (item.Param1 != null) ? item.Param1.Type : item.Param2.Type;
                    info1 = item.getInfo(1, prefix: indxStr1, doChildren: false);
                    info2 = item.getInfo(2, prefix: indxStr2, doChildren: false);
                    if (all) {
                        info += BrushParam.indented(key, level);
                        info += info1;
                        info += info2;
                    } else {
                        int index1 = info1.IndexOf(indxStr1);
                        if (index1 != -1) {
                            pos1 = index1 + indxStr1.Length;
                        } else {
                            pos1 = 0;
                        }
                        int index2 = info2.IndexOf(indxStr2);
                        if (index2 != -1) {
                            pos2 = index2 + indxStr2.Length;
                        } else {
                            pos2 = 0;
                        }
                        if (!info1.Substring(pos1).Equals(info2.Substring(pos2))) {
                            info += BrushParam.indented(key, level);
                            info += info1;
                            info += info2;
                        } else if (type == ParamType.JOBJECT) {
                            // info1 and info2 will be equal in this case
                            // Note lowercase equals, which is a BrushParam method
                            bool match = item.Param1 != null && item.Param2 != null &&
                                item.Param1.equals(item.Param2);
                            if (!match) {
                                info += BrushParam.indented(key, level);
                            }
                        }
                    }
                    if (item.Children.Count > 0) {
                        info += getItemInfo(item.Children, all);
                    }
                } else {
                    info += BrushParam.indented("  1 Error: ", item.Param1.Level);
                    info += BrushParam.indented("  2 Error: ", item.Param2.Level);
                }
            }
            return info;
        }

        BrushParam checkIfContained(BrushParam param, List<BrushParam> list) {
            foreach (BrushParam param2 in list) {
                if (param.Name == param2.Name) {
                    return param2;
                }
            }
            return null;
        }

        /// <summary>
        /// Searchs textBoxInfo for "Error";
        /// </summary>
        /// <returns></returns>
        private bool checkForErrors() {
            int index = textBoxInfo.Find("Error ",
                RichTextBoxFinds.MatchCase & RichTextBoxFinds.NoHighlight);
            return index != -1;
        }

        private void getFileName(FileType type) {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "Select a File" + " (" + type + ")";
            string fileName = "";
            // Set initial directory
            switch (type) {
                case FileType.Brush1:
                    //dlg.Filter = "Krita Presets|*.kpp";
                    fileName = textBoxBrush1.Text;
                    break;
                case FileType.Brush2:
                    //dlg.Filter = "Krita Presets|*.kpp";
                    fileName = textBoxCrush2.Text;
                    break;
            }
            if (File.Exists(fileName)) {
                dlg.FileName = fileName;
                dlg.InitialDirectory = Path.GetDirectoryName(fileName);
            }
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                resetFileName(type, dlg.FileName);
            }
        }

        private void resetFileName(FileType type, string name) {
            switch (type) {
                case FileType.Brush1:
                    textBoxBrush1.Text = name;
                    Properties.Settings.Default.DatabaseName1 = name;
                    break;
                case FileType.Brush2:
                    textBoxCrush2.Text = name;
                    Properties.Settings.Default.DatabaseName2 = name;
                    break;
            }
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Sets the global values for info and paramsList.
        /// </summary>
        /// <param name="fileType"></param>
        /// <param name="info"></param>
        /// <param name="paramsList"></param>
        private void registerOutput(FileType fileType, string info,
            string header, List<BrushParam> paramsList) {
            switch (fileType) {
                case FileType.Brush1:
                    info1 = info;
                    header1 = header;
                    paramsList1 = paramsList; ;
                    break;
                case FileType.Brush2:
                    info2 = info;
                    header2 = header;
                    paramsList2 = paramsList; ;
                    break;
            }
        }

        /// <summary>
        /// Outputs a heading in testBoxInfo according to the type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private void printHeading(FileType type) {
            switch (type) {
                case FileType.Brush1:
                    appendInfo(header1);
                    break;
                case FileType.Brush2:
                    appendInfo(header2);
                    break;
            }
        }

        /// <summary>
        /// Processes images to be included in the RTF.  Generates an RTF
        /// string and inserts it into textBoxInfo.
        /// </summary>
        /// <param name="images"></param>
        private void processImagesUsingRtf(List<Bitmap> images) {
            try {
                appendInfo("    ");
                String rtf;
                foreach (Bitmap bm in images) {
                    rtf = Utils.RTFUtils.imageRtf(textBoxInfo, bm);
                    if (!String.IsNullOrEmpty(rtf)) {
                        Utils.RTFUtils.appendRtb(textBoxInfo, rtf);
                        appendInfo("    ");
                    }
                }
                appendInfo(NL);
            } catch (Exception ex) {
                Utils.Utils.excMsg("Error processing effector images", ex);
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e) {
            Properties.Settings.Default.DatabaseName1 = textBoxBrush1.Text;
            Properties.Settings.Default.DatabaseName2 = textBoxCrush2.Text;
            Properties.Settings.Default.Save();
        }

        private void appendInfo(string info) {
            textBoxInfo.AppendText(info);
        }

        /// <summary>
        /// Appends the given text, handling any embedded images. The embedded
        /// images are of the form:<BR>
        /// BrushParam.Delim<RTF code></RTF>BrushParam.Delim
        /// </summary>
        /// <param name="text">The text containing embedded images.</param>
        private void appendInfoWithImages(string text) {
            string info = "";
            // Parse the info to get the images so they can be inserted
            int evenodd = 1;
            if (text.StartsWith(BrushParam.Delim.ToString())) {
                // The text starts with an image
                evenodd = 0;
            }
            string[] tokens = text.Split(BrushParam.Delim);
            Font oldFont = textBoxInfo.SelectionFont;
            for (int i = 0; i < tokens.Length; i++) {
                string token = tokens[i];
                if (i % 2 == evenodd) {
                    // Token is base64 image
                    appendInfo(NL + "    ");
                    try {
                        string base64 = generateRtfImage(token);
                        Utils.RTFUtils.insertRtb(textBoxInfo, base64);
                    } catch (Exception ex) {
                        string msg = "Error parsing image";
                        //Utils.Utils.excMsg(msg, ex);
                        appendInfo("!!! " + msg + NL);
                        //appendInfo("    " + ex.ToString());
                        //appendInfo("    " + ex.GetType());
                        appendInfo("    " + ex.Message);
                    }
                    textBoxInfo.SelectionFont = oldFont;
                    appendInfo("    ");
                    //appendInfo("<Image>");
                } else {
                    appendInfo(token);
                }
            }
        }

        //private void appendImages(BrushParam param) {
        //    if (param.Name.ToLower().Contains("effector")) {
        //        List<Bitmap> images = param.getEffectorImages("  ");
        //        if (images != null && images.Count > 0) {
        //            // Insert RTF string
        //            processImagesUsingRtf(images);
        //        }
        //    }
        //}

        /// <summary>
        /// Inserts the given text at the start of textBoxInfo.
        /// </summary>
        /// <param name="text"></param>
        private void InsertAtInfoTop(string text) {
            if (String.IsNullOrEmpty(text)) return;
            textBoxInfo.SelectionStart = 0;
            textBoxInfo.SelectionLength = 0;
            textBoxInfo.SelectedText = text;
        }

        private string getFileInfo(IReadOnlyList<MetadataExtractor.Directory> directories) {
            StringBuilder info = new StringBuilder();
            foreach (MetadataExtractor.Directory directory in directories) {
                if (directory.Name.Equals("File")) {
                    foreach (Tag tag in directory.Tags) {
                        info.Append(tag.Name).Append(" : ")
                            .Append(tag.Description).Append(NL);
                    }
                    return info.ToString();
                }
            }
            return null;
        }

        private void OnProcess1Click(object sender, EventArgs e) {
            processBrush(FileType.Brush1, true);
            textBoxInfo.Clear();
            printHeading(FileType.Brush1);
            // Append the params.info()
            foreach (BrushParam param in paramsList1) {
                // Parse the info to get the images so they can be inserted
                appendInfoWithImages(param.info());
            }
            // Check for errors
            if (checkForErrors()) {
                InsertAtInfoTop("!!! Note: There were errors during processing"
                    + NL + NL);
            }
        }

        private void OnProcess2Click(object sender, EventArgs e) {
            processBrush(FileType.Brush2, true);
            textBoxInfo.Clear();
            printHeading(FileType.Brush2);
            // Append the params.info()
            foreach (BrushParam param in paramsList2) {
                // Parse the info to get the images so they can be inserted
                appendInfoWithImages(param.info());
            }
            // Check for errors
            if (checkForErrors()) {
                InsertAtInfoTop("!!! There are errors" + NL + NL);
            }
        }

        private void OnCompareClick(object sender, EventArgs e) {
            compare(false);
            // Check for errors
            if (checkForErrors()) {
                InsertAtInfoTop("!!! There are errors" + NL + NL);
            }
        }

        private void OnCompareAllClick(object sender, EventArgs e) {
            compare(true);
            // Check for errors
            if (checkForErrors()) {
                InsertAtInfoTop("!!! There are errors" + NL + NL);
            }
        }

        private void OnQuitClick(object sender, EventArgs e) {
            Close();
        }

        private void OnOverviewClick(object sender, EventArgs e) {
            // Create, show, or set visible the overview dialog as appropriate
            if (overviewDlg == null) {
                MainForm app = (MainForm)FindForm().FindForm();
                overviewDlg = new ScrolledHTMLDialog(
                    Utils.Utils.getDpiAdjustedSize(app, new Size(800, 600)));
                overviewDlg.Show();
            } else {
                overviewDlg.Visible = true;
            }
        }

        private void OnAboutClick(object sender, EventArgs e) {
            AboutBox dlg = new AboutBox();
            dlg.ShowDialog();
        }

        private void OnBrowseBrush1Click(object sender, EventArgs e) {
            getFileName(FileType.Brush1);
        }

        private void OnBrowseBrush2Click(object sender, EventArgs e) {
            getFileName(FileType.Brush2);
        }

        private void OnSaveRtfClick(object sender, EventArgs e) {
            if (textBoxInfo == null) {
                return;
            }
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "RTF Files|*.rtf";
            dlg.Title = "Save as RTF";
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                try {
                    textBoxInfo.SaveFile(dlg.FileName,
                        RichTextBoxStreamType.RichText);
                } catch (Exception ex) {
                    Utils.Utils.excMsg("Error saving RTF", ex);
                }
            }
        }

        private void OnFindClick(object sender, EventArgs e) {
            if (textBoxInfo == null) {
                return;
            }
            if (findDlg == null) {
                findDlg = new FindDialog(textBoxInfo);
                // Keep it on top
                findDlg.Owner = this;
                findDlg.Show();
            } else {
                findDlg.Visible = true;
            }
        }

        private void OnShowToolHierarchy(object sender, EventArgs e) {
            //string label = sender.ToString();
            //string database;
            //if (label.StartsWith("Database 2")) {
            //    database = textBoxCrush2.Text;
            //} else {
            //    database = textBoxBrush1.Text;
            //}
            //string info = DatabaseUtils.getToolHierarchy(database);
            //// Create, show, or set visible the overview dialog as appropriate
            //if (textDlg == null) {
            //    MainForm app = (MainForm)FindForm().FindForm();
            //    textDlg = new ScrolledRichTextDialog(
            //        Utils.Utils.getDpiAdjustedSize(app, new Size(600, 400)),
            //        info);
            //    textDlg.Text = "Tool Hierarchy";
            //    textDlg.Show();
            //} else {
            //    textDlg.Visible = true;
            //}
        }

        // RichTextBox context menu
        private void OnCutClick(object sender, EventArgs e) {
            textBoxInfo.Cut();
        }

        private void OnCopyClick(object sender, EventArgs e) {
            textBoxInfo.Copy();
        }

        private void OnPasteClick(object sender, EventArgs e) {
            textBoxInfo.Paste();
        }

        private void OnSelectAllClick(object sender, EventArgs e) {
            textBoxInfo.SelectAll();
        }

        private void OnKeyDownPressed(object sender, KeyEventArgs e) {
            if (e.Control && e.KeyCode == Keys.F) {
                OnFindClick(sender, e);
            }
        }
    }

    public class NodeInfo {
        string nodeName;
        int nodeVariantId;
        int nodeInitVariantId;

        public string NodeName { get => nodeName; set => nodeName = value; }
        public int NodeVariantId { get => nodeVariantId; set => nodeVariantId = value; }
        public int NodeInitVariantId { get => nodeInitVariantId; set => nodeInitVariantId = value; }


        public NodeInfo(string nodeName, int nodeVariantId, int nodeInitVariantId) {
            NodeName = nodeName;
            NodeVariantId = nodeVariantId;
            NodeInitVariantId = nodeInitVariantId;
        }

        public string Info() {
            return nodeName + " NodeVariantId=" + nodeVariantId
                + " NodeInitVariantId=" + nodeInitVariantId;
        }
    }
}