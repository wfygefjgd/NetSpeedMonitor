using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.NetworkInformation;

class Program
{
    static long prevR, prevS;
    static DateTime prevT;
    static Form form;
    static bool dragging;
    static int dx, dy;

    [STAThread]
    static void Main()
    {
        long r = 0, sent = 0;
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            {
                var s = ni.GetIPv4Statistics();
                r += s.BytesReceived;
                sent += s.BytesSent;
            }
        prevR = r;
        prevS = sent;
        prevT = DateTime.Now;

        form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            TopMost = true,
            Opacity = 0.9,
            BackColor = Color.FromArgb(0x1e, 0x1e, 0x1e),
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        form.Load += (o, e) =>
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            form.Location = new Point(wa.Left, wa.Bottom - form.Height);
        };

        var label = new Label
        {
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(0, 255, 0),
            BackColor = Color.FromArgb(0x1e, 0x1e, 0x1e),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "\u25bc 0.00 MB/s   \u25b2 0.00 MB/s"
        };
        form.Controls.Add(label);
        BindDrag(label);
        BindDrag(form);

        var timer = new Timer { Interval = 1000 };
        timer.Tick += (o, e) =>
        {
            long nr = 0, ns = 0;
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var st = ni.GetIPv4Statistics();
                    nr += st.BytesReceived;
                    ns += st.BytesSent;
                }
            double dt = (DateTime.Now - prevT).TotalSeconds;
            if (dt > 0)
            {
                double d = Math.Max(0, (nr - prevR) / dt / 1048576.0);
                double u = Math.Max(0, (ns - prevS) / dt / 1048576.0);
                double m = Math.Max(d, u);
                label.ForeColor = m > 10 ? Color.Red : m > 5 ? Color.FromArgb(255, 136, 0) : Color.FromArgb(0, 255, 0);
                label.Text = "\u25bc " + d.ToString("N2") + " MB/s   \u25b2 " + u.ToString("N2") + " MB/s";
            }
            prevR = nr;
            prevS = ns;
            prevT = DateTime.Now;
        };
        timer.Start();
        Application.Run(form);
    }

    static void BindDrag(Control c)
    {
        c.MouseDown += (o, e) =>
        {
            if (e.Button == MouseButtons.Left) { dragging = true; dx = e.X; dy = e.Y; }
        };
        c.MouseMove += (o, e) =>
        {
            if (dragging) form.Location = new Point(form.Left + e.X - dx, form.Top + e.Y - dy);
        };
        c.MouseUp += (o, e) =>
        {
            if (e.Button == MouseButtons.Left) dragging = false;
        };
        c.MouseClick += (o, e) =>
        {
            if (e.Button == MouseButtons.Right) form.Close();
        };
    }
}
