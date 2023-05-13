using KEUtils.Utils;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RebelleBrushInfo {
    public class RebelleCurve {
        public static readonly String NL = Environment.NewLine;

        public bool fromParams;
        public double maximum;
        public double minimum;
        public double multiplier;
        public double outputMax;
        public double outputMin;
        public RebellePoint[] points;

        /// <summary>
        /// Reads a string that is the Value of a BrushParam with ParamType 
        /// CURVE and creates a RebelleCurve.
        /// </summary>
        /// <param name=Json string></param>
        /// <returns></returns>
        public static RebelleCurve getRebelleCurve(string json) {
            try {
                RebelleCurve newConfig = JsonConvert.
                    DeserializeObject<RebelleCurve>(json);
                return newConfig;
            } catch (Exception ex) {
                Utils.excMsg("Error reading configuration from "
                     + json, ex);
                return null;
            }
        }

        /// <summary>
        /// Creates formatted output for a RebelleCurve from the give Json string.
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static string formatCurve(string tab, string json) {
            RebelleCurve curve = RebelleCurve.getRebelleCurve(json);
            string info = "";
            info += tab + "fromParams: " + curve.fromParams + NL;
            info += tab + "maximum: " + curve.maximum + NL;
            info += tab + "minimum: " + curve.minimum + NL;
            info += tab + "multiplier: " + curve.multiplier + NL;
            info += tab + "outputMax: " + curve.outputMax + NL;
            info += tab + "outputMin: " + curve.outputMin + NL;
            RebellePoint[] points = curve.points;
            info += tab + "points (" + points.Length + "): " + NL;
            foreach (RebellePoint point in points) {
                info += tab + "  " + $"{point.x,10:N6}" + ", " + $"{point.x,10:N6}" + NL;
            }
            return info;
        }

        /// <summary>
        /// Creates an image that shows the effector graph for the given control points.
        /// </summary>
        /// <param name="controlPoints">The control points for the effector.</param>
        /// <returns></returns>
        public static Bitmap getCurveImage(RebelleCurve curve) {
            // DEBUG
            //return new Bitmap(@"C:\Users\evans\Pictures\Icon Images\BlueMouse.256x256.png");

            // Convert RebellePoint to PointF
            RebellePoint[] rebellePoints = curve.points;
            int nPoints = rebellePoints.Length;
            PointF[] controlPoints = new PointF[nPoints];
            for (int i = 0; i < nPoints; i++) {
                float offset = 1;
                if (curve.outputMax != curve.outputMin) {
                    offset = (float)(-curve.outputMin /
                        (curve.outputMax - curve.outputMin));
                }
                float x = rebellePoints[i].x;
                float y = (float)(rebellePoints[i].y + offset);
                controlPoints[i] = new PointF(x, y);
            }

            int margin = 0;
            int w = MainForm.ImageWidth, h = w;
            float scale = w - 2 * margin;
            Bitmap bm = new Bitmap(w, h);
            // RTfUtils uses this to set the size
            float sizeInches = 1.0f;
            bm.SetResolution(w / sizeInches, h / sizeInches);
            using (Graphics g = Graphics.FromImage(bm)) {
                // Scale it so we can work in [0,1] coordinate axes,
                // with y increasing up
                Matrix m = new System.Drawing.Drawing2D.Matrix();
                m.Scale(1f, -1f);
                m.Translate(0, -h);
                m.Translate(margin, margin);
                m.Scale(scale, scale);
                g.Transform = m;
                g.Clear(Color.FromArgb(180, 180, 180));
                // Grid lines
                using (Pen pen = new Pen(Color.FromArgb(164, 164, 164), 1 / scale)) {
                    for (int i = 1; i < 10; i++) {
                        g.DrawLine(pen, .125f * i, 0, .125f * i, 1);
                        g.DrawLine(pen, 0, .125f * i, 1, .125f * i);
                    }
                }
                // Axes
                using (Pen pen = new Pen(Color.Black, 1 / scale)) {
                    g.DrawLine(pen, 0, 0, 1, 0);
                    g.DrawLine(pen, 1, 0, 1, 1);
                    g.DrawLine(pen, 1, 1, 0, 1);
                    g.DrawLine(pen, 0, 1, 0, 0);
                }
                // Control points
                using (Brush brush = new SolidBrush(Color.FromArgb(123, 123, 123))) {
                    // Scale to default width of 256, Should be odd
                    int width = (int)(2 * Math.Floor(19.0F / 2.0F * MainForm.ImageWidth / 256.0F) + 1.0F);
                    int off = (width - 1) / 2;
                    foreach (PointF point in controlPoints) {
                        g.FillEllipse(brush,
                            new RectangleF(point.X - off / scale, point.Y - off / scale,
                            width / scale, width / scale));
                    }
                }
                // Curves
                using (Pen pen = new Pen(Color.Black, 2 / scale)) {
                    if (nPoints == 1) {
                        PointF x0 = new PointF(0.0F, controlPoints[0].Y);
                        PointF y0 = new PointF(1.0F, controlPoints[0].Y);
                        g.DrawLine(pen, x0, y0);
                    } else if (nPoints == 2) {
                        // TODO: Need to interpolate this to 0 and 1
                        PointF y0 = new PointF(1.0F, controlPoints[0].Y);
                        g.DrawLine(pen, controlPoints[0], controlPoints[1]);
                    } else {
                        // Use cubic spline
                        float[] xSpline = new float[nPoints];
                        float[] ySpline = new float[nPoints];
                        for (int i = 0; i < nPoints; i++) {
                            xSpline[i] = controlPoints[i].X;
                            ySpline[i] = controlPoints[i].Y;

                        }
                        float[] xFit, yFit;
                        int nFitPoints = 25;
                        CubicSpline.CubicSpline.FitParametric(xSpline, ySpline,
                            nFitPoints, out xFit, out yFit);
                        float xPrev = xFit[0];
                        float yPrev = yFit[0];
                        for (int i = 1; i < nFitPoints; i++) {
                            g.DrawLine(pen, xPrev, yPrev, xFit[i], yFit[i]);
                            xPrev = xFit[i];
                            yPrev = yFit[i];
                        }
                    }
                    // Add lines at left and right
                    if (nPoints >= 2) {
                        if (controlPoints[0].X > 0) {
                            PointF x0 = new PointF(0.0F, controlPoints[0].Y);
                            PointF y0 = new PointF(controlPoints[0].X, controlPoints[0].Y);
                            g.DrawLine(pen, x0, y0);
                        }
                        if (controlPoints[nPoints - 1].X < 1) {
                            PointF x0 = new PointF(controlPoints[nPoints - 1].X, controlPoints[nPoints - 1].Y);
                            PointF y0 = new PointF(1.0F, controlPoints[nPoints - 1].Y);
                            g.DrawLine(pen, x0, y0);
                        }
                    }
                }
            }
            //bm.Save(@"C:\Scratch\AAA\" + "RebelleCurveTest.png", ImageFormat.Png);
            return bm;
        }

    }

    public class RebellePoint {
        public float x;
        public float y;
    }
}
