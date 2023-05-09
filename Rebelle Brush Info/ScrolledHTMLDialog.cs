using System;
using System.Drawing;
using System.Windows.Forms;

namespace RebelleBrushInfo {
    public partial class ScrolledHTMLDialog : Form {
        public ScrolledHTMLDialog(Size size) {
            InitializeComponent();

            // Resize the Form
            if (size != null) {
                this.Size = size;
            }

            // Add the HTML
            string appDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            webView.Source = new Uri(System.IO.Path.Combine(appDir, @"Help\Overview.html"));
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e) {
            // Just hide rather than close if the user did it
            if (e.CloseReason == CloseReason.UserClosing) {
                e.Cancel = true;
                Visible = false;
            }
        }

        private void OnButtonBackClick(object sender, EventArgs e) {
            webView.GoBack();
        }

        private void OnButtonForwardClick(object sender, EventArgs e) {
            webView.GoForward();
        }

        private void OnButtonCancelClick(object sender, EventArgs e) {
            this.Visible = false;
        }
    }
}
