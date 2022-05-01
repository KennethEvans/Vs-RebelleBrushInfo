﻿//#define debugging
//#define replaceDoctype
//#define TEST

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using About;
using RebelleUtils;
using MetadataExtractor;
using System.Text;

namespace RebelleBrushInfo {

    public partial class MainForm : Form {
        enum FileType { Brush1, Brush2 };
        public static readonly int PROCESS_TIMEOUT = 5000; // ms
        public static readonly String NL = Environment.NewLine;
        private static ScrolledHTMLDialog overviewDlg;
        private static ScrolledRichTextDialog textDlg;
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
        private void compare() {
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

            // Look for items in 2 that are in 1
            BrushParam foundParam = null;
            foreach (BrushParam param1 in paramsList1) {
                foundParam = checkIfContained(param1, paramsList2);
                if (foundParam == null) {
                    appendInfo(param1.Name + NL);
                    appendInfo(param1.info("  1: ", false));
                    appendInfo(indented("  2: Not found in 1", param1.Level));
                    continue;
                }
                // !!!
                if (foundParam != null && !param1.equals(foundParam)) {
                    // Most interesting 1 and 2 are found and differ
                    appendInfo(param1.Name + NL);
                    if (param1.Children == null && foundParam.Children == null) {
                        // 1 and 2 both do not have children
                        appendInfo("  1: " + param1.info(doChildren: false));
                        appendInfo("  2: " + foundParam.info(doChildren: false));
                    } else if (param1.Children != null && foundParam.Children == null) {
                        // 1 has children, 2 does not
                        appendInfo(param1.info("  1: ", doChildren: true));
                        appendInfo(indented("  2: Not found in 1", param1.Level));
                    } else if (param1.Children == null && foundParam.Children != null) {
                        // 1 does not have children, 2 does
                        appendInfo(indented("  1: Not found in 2", foundParam.Level));
                        appendInfo(foundParam.info("  2: ", doChildren: true));
                    } else {
                        // Both have children
                        // Only process first level of children
                        // Look for the same name
                        BrushParam foundParam1 = null;
                        foreach (BrushParam param11 in param1.Children) {
                            foundParam1 = checkIfContained(param11, foundParam.Children);
                            if (foundParam1 == null) {
                                // 2 not in 1 (children)
                                appendInfo(param11.Name + NL);
                                appendInfo(param11.info("  1: ", true));
                                appendInfo(indented("  2: Not found in 1", param11.Level));
                                continue;
                            }
                            if (foundParam1 != null && !param11.equals(foundParam1)) {
                                // 1 and 2 both there and differ (children)
                                appendInfo(param11.Name + NL);
                                appendInfo(param11.info("1: ", true));
                                appendInfo(foundParam1.info("2: ", true));
                            }
                        }
                        //// Look for items in 2 that are not in 1
                        //if (foundParam1 != null) {
                        //    foreach (BrushParam param22 in foundParam.Children) {
                        //        foundParam1 = checkIfContained(param22, param1.Children);
                        //        if (foundParam1 == null) {
                        //            // 1 not in 2 (children)
                        //            appendInfo(foundParam.Name + NL);
                        //            appendInfo(indented("  1: Not found in 2", param22.Level));
                        //            appendInfo(param22.info("  2: ", false));
                        //            continue;
                        //        }
                        //    }
                        //}
                    }
                }
            }

            // Look for items in 2 that are not in 1
            foreach (BrushParam param2 in paramsList2) {
                foundParam = checkIfContained(param2, paramsList1);
                if (foundParam == null) {
                    appendInfo(param2.Name + NL);
                    appendInfo(indented("  1: Not found in 2", param2.Level));
                    appendInfo(param2.info("  2: ", false));
                    continue;
                }
            }
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
        /// Returns a string that is indented like BrushParam.info is. A NL is
        /// added;
        /// </summary>
        /// <param name="text"></param>
        /// <param name="level">The level to use for indentation. Get from a
        /// BrushParam</param>
        /// <param name="tab">The tab string to use, usually "    "</param>
        /// <returns>The indented string.</returns>
        private string indented(string text, int level, string tab = "    ") {
            string info = "";
            for (int i = 1; i < level; i++) {
                info += tab;
            }
            info += text + NL;
            return info;
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
            foreach (BrushParam param in paramsList1) {
                appendInfo(param.info());
                //appendImages(param);
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
            foreach (BrushParam param in paramsList2) {
                appendInfo(param.info());
                //appendImages(param);
            }
            // Check for errors
            if (checkForErrors()) {
                InsertAtInfoTop("!!! There are errors" + NL + NL);
            }
        }

        private void OnCompareClick(object sender, EventArgs e) {
            compare();
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

        private void OnBrowseDatabase1Click(object sender, EventArgs e) {
            getFileName(FileType.Brush1);
        }

        private void OnBrowseDatabase2Click(object sender, EventArgs e) {
            getFileName(FileType.Brush2);
        }

        private void OnBrowseBrush1Click(object sender, EventArgs e) {
            string databaseName = textBoxBrush1.Text;
            if (databaseName == null || databaseName.Length == 0) {
                Utils.Utils.errMsg("Brush 1 is not defined");
                return;
            }
            if (!File.Exists(databaseName)) {
                Utils.Utils.errMsg("Brush 1 does not exist");
                return;
            }
        }

        private void OnBrowseBrush2Click(object sender, EventArgs e) {
            string databaseName = textBoxCrush2.Text;
            if (databaseName == null || databaseName.Length == 0) {
                Utils.Utils.errMsg("Brush 2 is not defined");
                return;
            }
            if (!File.Exists(databaseName)) {
                Utils.Utils.errMsg("Brush 2 does not exist");
                return;
            }
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