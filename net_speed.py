import sys
import time
import tkinter as tk
import tkinter.font as tkfont
import ctypes
from ctypes import wintypes

# ── 第一步：pdh.dll 温度读取（零外部依赖） ──────────────────
pdh = ctypes.windll.pdh

PdhOpenQuery = pdh.PdhOpenQueryW
PdhOpenQuery.argtypes = [wintypes.LPCWSTR, wintypes.LPVOID, ctypes.POINTER(wintypes.HANDLE)]
PdhOpenQuery.restype = wintypes.LONG

PdhAddEnglishCounter = pdh.PdhAddEnglishCounterW
PdhAddEnglishCounter.argtypes = [wintypes.HANDLE, wintypes.LPCWSTR, wintypes.LPVOID, ctypes.POINTER(wintypes.HANDLE)]
PdhAddEnglishCounter.restype = wintypes.LONG

PdhCollectQueryData = pdh.PdhCollectQueryData
PdhCollectQueryData.argtypes = [wintypes.HANDLE]
PdhCollectQueryData.restype = wintypes.LONG

PdhGetFormattedCounterValue = pdh.PdhGetFormattedCounterValue
PdhGetFormattedCounterValue.argtypes = [wintypes.HANDLE, wintypes.DWORD, ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
PdhGetFormattedCounterValue.restype = wintypes.LONG

PdhCloseQuery = pdh.PdhCloseQuery
PdhCloseQuery.argtypes = [wintypes.HANDLE]
PdhCloseQuery.restype = wintypes.LONG

PdhExpandWildCardPath = pdh.PdhExpandWildCardPathW
PdhExpandWildCardPath.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD), wintypes.DWORD]
PdhExpandWildCardPath.restype = wintypes.LONG

class PDH_FMT_COUNTERVALUE(ctypes.Structure):
    _fields_ = [("CStatus", wintypes.DWORD), ("doubleValue", ctypes.c_double)]

PDH_FMT_DOUBLE = 0x00000200
PDH_MORE_DATA = 0x800007D2

def _uret(ret):
    return ret & 0xFFFFFFFF if ret < 0 else ret

class TempMonitor:
    def __init__(self):
        self.query = wintypes.HANDLE()
        self.counters = []
        self._ok = False
        self._init_pdh()

    def _init_pdh(self):
        ret = PdhOpenQuery(None, None, ctypes.byref(self.query))
        if ret != 0:
            return
        path = r"\Thermal Zone Information(*)\Temperature"
        buf_size = wintypes.DWORD(0)
        ret = PdhExpandWildCardPath(None, path, None, ctypes.byref(buf_size), 0)
        if _uret(ret) == PDH_MORE_DATA:
            buf = ctypes.create_unicode_buffer(buf_size.value)
            ret = PdhExpandWildCardPath(None, path, buf, ctypes.byref(buf_size), 0)
            if ret != 0:
                return
            full = ctypes.wstring_at(ctypes.addressof(buf), buf_size.value)
            paths = [p for p in full.split('\0') if p]
            if not paths:
                return
            for p in paths:
                h = wintypes.HANDLE()
                if PdhAddEnglishCounter(self.query, p, None, ctypes.byref(h)) == 0:
                    self.counters.append(h)
        if self.counters:
            self._ok = True
            PdhCollectQueryData(self.query)

    def read(self):
        if not self._ok:
            return -1
        ret = PdhCollectQueryData(self.query)
        if ret != 0:
            return -1
        total = 0.0
        count = 0
        for h in self.counters:
            val = PDH_FMT_COUNTERVALUE()
            dw = wintypes.DWORD()
            if PdhGetFormattedCounterValue(h, PDH_FMT_DOUBLE, ctypes.byref(dw), ctypes.byref(val)) == 0:
                c = val.doubleValue / 10.0 - 273.15
                if 0 < c < 110:
                    total += c
                    count += 1
        return total / count if count > 0 else -1

    def close(self):
        if self.query.value:
            PdhCloseQuery(self.query)

# ── 第二步：网速（需要 pip install psutil） ────────────────
try:
    import psutil
except ImportError:
    print("错误: 需要安装 psutil，请运行: pip install psutil")
    sys.exit(1)

_prev_net = None
_prev_t = time.time()

def get_network_speed():
    global _prev_net, _prev_t
    now = time.time()
    cnt = psutil.net_io_counters()
    if _prev_net is None:
        _prev_net = cnt
        _prev_t = now
        return 0, 0
    dt = now - _prev_t
    if dt <= 0:
        return 0, 0
    down = max(0, (cnt.bytes_recv - _prev_net.bytes_recv) / dt)
    up = max(0, (cnt.bytes_sent - _prev_net.bytes_sent) / dt)
    _prev_net = cnt
    _prev_t = now
    return down, up

def fmt_speed(bps):
    if bps >= 1048576:
        return f"{bps/1048576:.2f} MB/s"
    elif bps >= 1024:
        return f"{bps/1024:.2f} KB/s"
    else:
        return f"{bps:.0f} B/s"

# ── 第三步：UI ──────────────────────────────────────────
root = tk.Tk()
root.overrideredirect(True)
root.attributes('-topmost', True)
root.attributes('-alpha', 0.9)
root.configure(bg='#1e1e1e')
root.geometry('+0+0')

f = tkfont.Font(family='Segoe UI', size=10)
label = tk.Label(root, font=f, fg='#00ff00', bg='#1e1e1e', anchor='center')
label.pack()

def on_press(e):
    root._dx = e.x
    root._dy = e.y
def on_move(e):
    root.geometry(f"+{root.winfo_x() + e.x - root._dx}+{root.winfo_y() + e.y - root._dy}")
def on_close(e):
    temp_mon.close()
    root.destroy()

label.bind('<Button-1>', on_press)
label.bind('<B1-Motion>', on_move)
label.bind('<Button-3>', on_close)
root.bind('<Button-1>', on_press)
root.bind('<B1-Motion>', on_move)
root.bind('<Button-3>', on_close)

temp_mon = TempMonitor()

def update():
    down, up = get_network_speed()
    temp = temp_mon.read()

    d = fmt_speed(down)
    u = fmt_speed(up)
    text = f"\u25bc {d}  \u25b2 {u}"
    if temp > 0:
        text += f"  CPU: {temp:.1f}\u00b0C"

    m = max(down, up)
    if m > 10 * 1048576:
        color = '#ff0000'
    elif m > 5 * 1048576:
        color = '#ff8800'
    else:
        color = '#00ff00'

    label.config(text=text, fg=color)
    root.after(1000, update)

root.after(200, update)
root.mainloop()
