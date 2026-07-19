using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

class Program
{
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhOpenQuery(string szDataSource, IntPtr dwUserData, out IntPtr phQuery);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhAddEnglishCounterW(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhCollectQueryData(IntPtr hQuery);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, out uint lpdwType, out PDH_FMT_COUNTERVALUE pValue);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhCloseQuery(IntPtr hQuery);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    static extern int PdhGetFormattedCounterArrayW(IntPtr hCounter, uint dwFormat, ref int lpdwBufferSize, out int lpdwItemCount, IntPtr itemBuffer);

    [StructLayout(LayoutKind.Explicit)]
    struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(4)] public double doubleValue;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct PDH_FMT_COUNTERVALUE_ITEM
    {
        [FieldOffset(0)] public IntPtr szName;
        [FieldOffset(4)] public PDH_FMT_COUNTERVALUE FmtValue;
    }

    const int PDH_MORE_DATA = unchecked((int)0x800007D2);
    const uint PDH_FMT_DOUBLE = 0x00000200;

    static long prevR, prevS;
    static DateTime prevT;
    static Label label;
    static Form form;
    static bool dragging;
    static int dx, dy;
    static bool hasTemp;

    [STAThread]
    static void Main()
    {
        long r = 0, sent = 0;
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                { var s = ni.GetIPv4Statistics(); r += s.BytesReceived; sent += s.BytesSent; }
        prevR = r; prevS = sent; prevT = DateTime.Now;

        double testTemp = GetCpuAverageTemperature();
        hasTemp = testTemp > 0;

        form = new Form();
        form.FormBorderStyle = FormBorderStyle.None;
        form.TopMost = true;
        form.Opacity = 0.9;
        form.BackColor = Color.FromArgb(0x1e, 0x1e, 0x1e);
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(0, 0);
        form.AutoSize = true;
        form.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        label = new Label();
        label.Font = new Font("Segoe UI", 10);
        label.ForeColor = Color.FromArgb(0, 255, 0);
        label.BackColor = Color.FromArgb(0x1e, 0x1e, 0x1e);
        label.AutoSize = true;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Text = "\u25bc 0.00 MB/s   \u25b2 0.00 MB/s";
        form.Controls.Add(label);

        label.MouseDown += (o, e) => { if (e.Button == MouseButtons.Left) { dragging = true; dx = e.X; dy = e.Y; } };
        label.MouseMove += (o, e) => { if (dragging) form.Location = new Point(form.Left + e.X - dx, form.Top + e.Y - dy); };
        label.MouseUp += (o, e) => { if (e.Button == MouseButtons.Left) dragging = false; };
        label.MouseClick += (o, e) => { if (e.Button == MouseButtons.Right) form.Close(); };

        form.MouseDown += (o, e) => { if (e.Button == MouseButtons.Left) { dragging = true; dx = e.X; dy = e.Y; } };
        form.MouseMove += (o, e) => { if (dragging) form.Location = new Point(form.Left + e.X - dx, form.Top + e.Y - dy); };
        form.MouseUp += (o, e) => { if (e.Button == MouseButtons.Left) dragging = false; };
        form.MouseClick += (o, e) => { if (e.Button == MouseButtons.Right) form.Close(); };

        Timer timer = new Timer();
        timer.Interval = 1000;
        timer.Tick += (o, e) =>
        {
            long nr = 0, ns = 0;
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    { var st = ni.GetIPv4Statistics(); nr += st.BytesReceived; ns += st.BytesSent; }
            double dt = (DateTime.Now - prevT).TotalSeconds;
            if (dt > 0)
            {
                double d = Math.Max(0, (nr - prevR) / dt / 1048576.0);
                double u = Math.Max(0, (ns - prevS) / dt / 1048576.0);
                double m = Math.Max(d, u);
                Color c = m > 10 ? Color.Red : m > 5 ? Color.FromArgb(255, 136, 0) : Color.FromArgb(0, 255, 0);
                string text = "\u25bc " + d.ToString("N2") + " MB/s   \u25b2 " + u.ToString("N2") + " MB/s";
                if (hasTemp) { double t = GetCpuAverageTemperature(); if (t > 0) text += "  " + t.ToString("F1") + "\u00b0C"; }
                label.Text = text;
                label.ForeColor = c;
            }
            prevR = nr; prevS = ns; prevT = DateTime.Now;
        };
        timer.Start();

        Application.Run(form);
    }

    static double GetCpuAverageTemperature()
    {
        IntPtr query;
        if (PdhOpenQuery(null, IntPtr.Zero, out query) != 0) return -1;
        try
        {
            IntPtr counter;
            if (PdhAddEnglishCounterW(query, @"\Thermal Zone Information(*)\Temperature", IntPtr.Zero, out counter) != 0)
                return -1;

            PdhCollectQueryData(query);
            System.Threading.Thread.Sleep(10);
            if (PdhCollectQueryData(query) != 0) return -1;

            int bufSize = 0, itemCount = 0;
            int ret = PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE, ref bufSize, out itemCount, IntPtr.Zero);
            if (ret != PDH_MORE_DATA || itemCount == 0) return -1;

            IntPtr buffer = Marshal.AllocHGlobal(bufSize);
            try
            {
                ret = PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE, ref bufSize, out itemCount, buffer);
                if (ret != 0) return -1;

                double sum = 0; int count = 0;
                int itemSize = Marshal.SizeOf(typeof(PDH_FMT_COUNTERVALUE_ITEM));
                for (int i = 0; i < itemCount; i++)
                {
                    IntPtr ptr = new IntPtr(buffer.ToInt64() + i * itemSize);
                    PDH_FMT_COUNTERVALUE_ITEM item = (PDH_FMT_COUNTERVALUE_ITEM)Marshal.PtrToStructure(ptr, typeof(PDH_FMT_COUNTERVALUE_ITEM));
                    double celsius = item.FmtValue.doubleValue / 10.0 - 273.15;
                    if (celsius > 0 && celsius < 110) { sum += celsius; count++; }
                }
                if (count > 0) return sum / count;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { PdhCloseQuery(query); }
        return -1;
    }
}
