<p align="center">
  <img src="docs/images/wenmind-logo.png" width="64" height="64" alt="WenMind 双引号标志">
</p>

<h1 align="center">WenMind</h1>

<p align="center">任务栏名言，随心可见。</p>

<p align="center">
  <a href="https://github.com/leavestring/WenMind-Taskbar/releases/latest">下载 Windows x64 便携版</a>
</p>

![WenMind 每日一言，雾山桌面上的任务栏与名言卡片](docs/images/wenmind-banner.png)

WenMind 是一个 Windows 11 后台小程序。打开后，中文名言就出现在主屏任务栏的空档里。工作或学习时抬眼能看到一句，想读完整内容时点一下就好。

## 实机效果

![Windows 11 任务栏实机截图，WenMind 在系统托盘左侧显示带双引号标志的名言短句](docs/images/wenmind-taskbar-demo.png)

任务栏只展示一行短句，长句会自动省略。点击短句可打开详情卡，查看全文、作者和出处，还可以复制或换一句。

## 功能

- **每天一条或定时刷新**。默认按系统本地日期更新，也可设置 **1–1440 分钟**的刷新间隔。
- **想换就换**。详情卡里的「换一句」会同时更新卡片和任务栏。固定间隔模式会从这次换句重新计时。
- **在线取句，离线也能看**。优先从一言获取文学和哲学句子，请求失败或遇到近期看过的句子时使用内置句库。
- **后台运行，随时开关**。从托盘切换任务栏文字、调整刷新间隔、设置登录时启动，或直接退出程序。

## 开始使用

1. 到 [最新 Release](https://github.com/leavestring/WenMind-Taskbar/releases/latest) 下载 Windows x64 ZIP，解压整个文件夹。
2. 双击 `WenMind.Daily.exe`。便携包附带所需的 .NET 运行时。
3. 右键托盘里的 WenMind 双引号图标，在「设置」中调整刷新频率。图标可能位于托盘的 `^` 隐藏图标菜单里。

停用时可在托盘菜单选择「隐藏任务栏文字」或「退出」。隐藏后仍可从托盘查看名言。当前名言和设置保存在 `%LocalAppData%\WenMind\Daily`，重启时会继续使用。

### 显示范围

目前支持 **Windows 11 主屏的底部任务栏**。当任务栏开启自动隐藏、屏幕进入全屏或可用空间不足时，可以从托盘查看名言。Windows 快速设置也可能暂时挡住文字。本机测试中，按 `Esc` 或 `Win+A` 关闭快速设置，比再次点击右下角的托盘按钮更容易立即恢复。

## 开发与构建

桌面应用使用 **C#、.NET 10 和 WPF**。安装 .NET 10 SDK 后，在仓库根目录执行以下命令。

```powershell
dotnet run --project windows/WenMind.Daily.csproj
```

构建自包含 Windows x64 便携包。

```powershell
dotnet publish windows/WenMind.Daily.csproj -c Release -r win-x64 --self-contained true -p:DebugType=None -o windows/dist/WenMind-Taskbar-win-x64
```

程序源码位于 [windows/](windows/)，使用与构建细节见 [Windows 说明](windows/README.md)。

## License

MIT License © 2026 WenMind Studio
