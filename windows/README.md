# WenMind 每日一言 · Windows 11

在主屏的 Windows 11 任务栏空档显示一句中文名言。点击任务栏上的文字或托盘里的 WenMind 图标，可查看全文、作者和出处。

## 使用

1. 解压便携包，双击 `WenMind.Daily.exe`。自包含包附带所需的 .NET 运行时。
2. 托盘里的双引号图标可能位于 Windows 的「显示隐藏的图标」菜单中。右键该图标可打开设置、显示或隐藏任务栏文字、切换登录时启动、退出。
3. 在「设置」里选择每天一次，或输入 1 到 1440 分钟的固定间隔。固定间隔从最近一次自动更新开始计算，重启后按缓存中的更新时间继续计算。
4. 详情卡可复制全文与出处，也可以点「换一句」浏览本地句库。任务栏继续显示当前自动刷新的名言。

每天一次模式按系统本地日期更新。程序优先使用[一言](https://developer.hitokoto.cn/)的哲理分类，超时、无法获取或返回上一句时使用内置中文句库。名言缓存和设置保存在 `%LocalAppData%\WenMind\Daily`。

任务栏文字是程序自己的透明窗口。当前版支持主屏底部任务栏。任务栏自动隐藏、全屏、空位不足，或 Windows 暂时无法提供可靠的任务栏布局时，文字会收起，仍可从托盘打开详情。

## 构建

从仓库根目录用 .NET 10 SDK 执行：

```powershell
dotnet build windows/WenMind.Daily.csproj
dotnet publish windows/WenMind.Daily.csproj -c Release -r win-x64 --self-contained true -p:DebugType=None -o windows/dist/WenMind-Daily-win-x64
```

便携包包含整个 `windows/dist/WenMind-Daily-win-x64` 目录。登录时启动使用当前可执行文件的位置，移动目录后可从托盘重新启用该选项。
