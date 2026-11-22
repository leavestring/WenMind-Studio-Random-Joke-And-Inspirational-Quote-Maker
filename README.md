<p align="center">
  <img src="docs/images/wenmind-logo.png" width="64" height="64" alt="WenMind 双引号标志">
</p>

<h1 align="center">WenMind</h1>

<p align="center">任务栏名言，随心可见。</p>

<p align="center">
  <a href="https://github.com/leavestring/WenMind-Taskbar/releases/latest">下载 Windows x64 便携版</a>
</p>

![WenMind 每日一言，雾山桌面上的任务栏与名言卡片](docs/images/wenmind-banner.png)

WenMind 是一个 Windows 11 后台小程序，在主屏底部任务栏的空档显示中文名言。抬眼看一句，点开读全文。

## 功能

- **随心刷新**。默认每天一条，也可以在托盘设置里把刷新间隔设为 1–1440 分钟。
- **点开读全文**。任务栏上只显示短句，点击后可以查看作者和出处、复制全文或手动换一句。
- **离线可用**。优先从一言获取文学和哲学句子，连接失败时使用内置句库。
- **随时开关**。右键托盘里的双引号图标，可以隐藏文字、设置登录时启动，也可以退出后台程序。

## 开始使用

1. 到 [最新 Release](https://github.com/leavestring/WenMind-Taskbar/releases/latest) 下载 Windows x64 ZIP，并将整个压缩包解压。
2. 双击 `WenMind.Daily.exe`。便携包附带所需的 .NET 运行时。
3. 右键托盘图标，进入「设置」调整刷新间隔。图标可能位于托盘的隐藏图标菜单中。

## 开发与构建

桌面应用使用 C#、.NET 10 和 WPF。安装 .NET 10 SDK 后，在项目根目录运行以下命令。

```powershell
dotnet run --project windows/WenMind.Daily.csproj
```

[查看 Windows 使用与便携包构建说明](windows/README.md)

## License

MIT License © 2026 WenMind Studio
