# NetSpeedMonitor

实时网速监控悬浮窗 — Windows 桌面小工具

## 版本

### C# 版（推荐）
`net_speed.cs` — 原生 Windows 应用，低资源占用，显示实时下载/上传速度和 CPU 温度。

编译：
```bash
csc net_speed.cs /reference:System.Windows.Forms.dll /reference:System.Drawing.dll
```

### Python 版
`net_speed.py` — Python tkinter 实现，功能相同。

```bash
python net_speed.py
```

## 功能

- 实时显示下载/上传速度（MB/s）
- CPU 温度监控（支持 Intel/AMD）
- 速度超限自动变色（绿→橙→红）
- 悬浮窗模式，始终置顶
- 鼠标拖拽移动位置
- 右键退出

## 免责声明

- 本软件仅供学习、研究和技术交流目的使用。
- 开发者不对软件的使用后果承担任何责任。
- 严禁将本软件用于任何商业用途或非法目的。
- 如您不同意以上条款，请立即停止使用并删除本软件。
