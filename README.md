# NetSpeedMonitor

实时网速监控悬浮窗 — Windows 桌面小工具

## 简介

C# 原生实现，极低资源占用，实时显示下载/上传速度和 CPU 温度。

## 功能

- 实时网速监控（下载/上传 MB/s）
- CPU 温度显示（支持 Intel/AMD）
- 速度超限自动变色：正常 ?? → 较高 ?? → 超限 ??
- 悬浮窗置顶，鼠标拖拽移动
- 右键退出

## 编译

`ash
csc net_speed.cs /reference:System.Windows.Forms.dll /reference:System.Drawing.dll
`

## 免责声明

- 本软件仅供学习、研究和技术交流目的使用。
- 开发者不对软件的使用后果承担任何责任。
- 严禁将本软件用于任何商业用途或非法目的。
- 如您不同意以上条款，请立即停止使用并删除本软件。

