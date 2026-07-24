using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Windows.Forms;

class Program
{
    static long prevR, prevS;
    static long prevTicks;
    static Form form;
    static bool dragging;
    static int dx, dy;

    static readonly string[] VirtualNameHints =
    {
        "virtual", "vmware", "vbox", "virtualbox", "hyper-v", "vethernet",
        "vpn", "tap", "tun", "wsl", "docker", "bluetooth", "teredo",
        "isatap", "6to4", "loopback", "pseudo", "wi-fi direct", "microsoft wi-fi direct",
        "npcap", "npcap loopback", "radmin", "hamachi", "zerotier", "wireguard",
        "openvpn", "softether", "clash", "meta", "wintun", "sstap"
    };

    [STAThread]
    static void Main()
    {
        long r, s;
        ReadTraffic(out r, out s);
        prevR = r;
        prevS = s;
        prevTicks = Stopwatch.GetTimestamp();

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
            long nr, ns;
            ReadTraffic(out nr, out ns);
            long now = Stopwatch.GetTimestamp();
            double dt = (now - prevTicks) / (double)Stopwatch.Frequency;
            if (dt > 0.2)
            {
                long dr = nr - prevR;
                long du = ns - prevS;
                if (dr < 0) dr = 0;
                if (du < 0) du = 0;
                double d = dr / dt / 1048576.0;
                double u = du / dt / 1048576.0;
                double m = Math.Max(d, u);
                label.ForeColor = m > 10 ? Color.Red : m > 5 ? Color.FromArgb(255, 136, 0) : Color.FromArgb(0, 255, 0);
                label.Text = "\u25bc " + d.ToString("N2") + " MB/s   \u25b2 " + u.ToString("N2") + " MB/s";
            }
            prevR = nr;
            prevS = ns;
            prevTicks = now;
        };
        timer.Start();
        Application.Run(form);
    }

    static void ReadTraffic(out long recv, out long sent)
    {
        recv = 0;
        sent = 0;
        List<NetworkInterface> candidates = new List<NetworkInterface>();
        foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!IsCountable(ni)) continue;
            candidates.Add(ni);
        }

        // Prefer adapters that actually route internet (have a gateway).
        List<NetworkInterface> withGw = new List<NetworkInterface>();
        foreach (NetworkInterface ni in candidates)
        {
            try
            {
                foreach (GatewayIPAddressInformation g in ni.GetIPProperties().GatewayAddresses)
                {
                    if (g.Address != null && !g.Address.Equals(System.Net.IPAddress.Any)
                        && !g.Address.Equals(System.Net.IPAddress.IPv6Any)
                        && !g.Address.Equals(System.Net.IPAddress.None))
                    {
                        withGw.Add(ni);
                        break;
                    }
                }
            }
            catch { }
        }

        IEnumerable<NetworkInterface> use = withGw.Count > 0 ? (IEnumerable<NetworkInterface>)withGw : candidates;
        foreach (NetworkInterface ni in use)
        {
            try
            {
                IPv4InterfaceStatistics st = ni.GetIPv4Statistics();
                recv += st.BytesReceived;
                sent += st.BytesSent;
            }
            catch { }
        }
    }

    static bool IsCountable(NetworkInterface ni)
    {
        if (ni.OperationalStatus != OperationalStatus.Up) return false;
        NetworkInterfaceType t = ni.NetworkInterfaceType;
        if (t == NetworkInterfaceType.Loopback || t == NetworkInterfaceType.Tunnel
            || t == NetworkInterfaceType.Ppp || t == NetworkInterfaceType.Unknown)
            return false;
        if (t != NetworkInterfaceType.Ethernet
            && t != NetworkInterfaceType.Wireless80211
            && t != NetworkInterfaceType.GigabitEthernet
            && t != NetworkInterfaceType.FastEthernetT
            && t != NetworkInterfaceType.FastEthernetFx
            && t != NetworkInterfaceType.Ethernet3Megabit)
            return false;

        string name = ((ni.Name ?? "") + " " + (ni.Description ?? "")).ToLowerInvariant();
        foreach (string h in VirtualNameHints)
            if (name.IndexOf(h, StringComparison.Ordinal) >= 0)
                return false;
        return true;
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
