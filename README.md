<p align="center">
  <img src="docs/images/wenmind-logo.png" width="64" height="64" alt="WenMind 双引号标志">
</p>

<h1 align="center">WenMind</h1>

<p align="center">在 Windows 11 任务栏上，读一句今日名言。</p>

![WenMind 每日一言横版宣传图，展示雾山桌面上的任务栏与名言卡片](docs/images/wenmind-banner.png)

## Windows 11 · 每日一言

WenMind 在主屏底部任务栏的空档显示中文名言。点击短句可看全文、作者和出处，也可以复制或换一句。

- **按你的节奏刷新**。默认每天一条，也可在托盘设置里输入 1–1440 分钟的刷新间隔。
- **断网也有内容**。优先从一言获取文学和哲学句子，连接失败或遇到近期重复句子时使用内置句库。
- **随时隐藏或退出**。右键托盘的 WenMind 图标可切换任务栏文字、登录时启动，或结束后台运行。

桌面版使用 C#、.NET 10 与 WPF。安装 .NET 10 SDK 后，在仓库根目录运行以下命令。

```powershell
dotnet run --project windows/WenMind.Daily.csproj
```

[查看 Windows 使用与便携包构建说明](windows/README.md)

## Web 版 · WenMind Studio

> 随机笑话与鸡汤文案生成收藏系统 — 获取每日份的快乐与温暖

![Python](https://img.shields.io/badge/Python-3.9+-blue)
![FastAPI](https://img.shields.io/badge/FastAPI-0.115-green)
![SQLite](https://img.shields.io/badge/SQLite-3-lightblue)
![License](https://img.shields.io/badge/License-MIT-orange)

---

### 📋 项目简介

WenMind Studio 是一个**前后端分离**的 Web 应用，提供：

- 😄 **随机笑话** — 从韩小韩 API 获取中文笑话
- 💪 **鸡汤文案** — 从一言获取中文名言
- ❤️ **收藏系统** — 收藏喜欢的文案，随时翻阅
- 🕐 **浏览历史** — 自动记录你的每一次阅读
- 🔐 **用户系统** — 注册登录，数据完全隔离

---

### 🏗 项目架构

```
wenmind-studio/
├── backend/                       # 后端 - Python FastAPI
│   ├── main.py                   # 应用入口，路由注册，CORS 配置
│   ├── database.py               # SQLite 数据库连接
│   ├── models.py                 # ORM 数据模型（User/Favorite/ViewHistory）
│   ├── schemas.py                # Pydantic 请求/响应模型
│   ├── auth.py                   # JWT 认证 + 密码加密
│   ├── routers/
│   │   ├── users.py              # 用户注册/登录
│   │   ├── content.py            # 笑话/鸡汤获取 + 浏览历史
│   │   └── favorites.py          # 收藏 CRUD
│   ├── services/
│   │   ├── joke_service.py       # 第三方笑话 API 调用
│   │   └── quote_service.py      # 第三方鸡汤 API 调用
│   ├── utils/
│   │   ├── response.py           # 统一 API 返回格式
│   │   └── exceptions.py         # 自定义异常 + 全局异常处理
│   └── requirements.txt          # Python 依赖
├── frontend/                      # 前端 - 原生 HTML + CSS + JS
│   ├── css/
│   │   └── style.css             # 全局样式（毛玻璃设计）
│   ├── js/
│   │   └── api.js                # API 请求封装（令牌管理、错误处理）
│   └── pages/
│       ├── login.html            # 登录页面
│       ├── register.html         # 注册页面
│       └── dashboard.html        # 主面板（展示 + 收藏 + 历史）
├── windows/                       # Windows 11 任务栏客户端
├── docs/images/                   # README 标志和宣传图
├── README.md                     # 本文件
└── API.md                        # API 接口文档
```

---

### 🚀 快速开始

#### 前置要求

- Python 3.9 或更高版本
- pip（Python 包管理器）
- 现代浏览器（Chrome / Firefox / Edge）

#### 1️⃣ 安装后端依赖

```bash
# 在项目根目录执行
pip install -r backend/requirements.txt
```

#### 2️⃣ 启动后端服务

```bash
# 在项目根目录执行
python -m uvicorn backend.main:app --reload --host 0.0.0.0 --port 8000
```

启动成功后，终端会显示：

```
[WenMind] 数据库初始化完成 ✓
[WenMind] WenMind Studio API 已启动
```

访问 http://localhost:8000/docs 可查看 Swagger API 文档。

#### 3️⃣ 打开前端页面

**方式一（推荐）：使用 VS Code Live Server**

1. 安装 VS Code 插件 `Live Server`
2. 右键 `frontend/pages/login.html` → `Open with Live Server`
3. 浏览器自动打开登录页面

**方式二：直接浏览器打开**

直接双击 `frontend/pages/login.html` 用浏览器打开（注意：部分浏览器可能因 CORS 限制无法正常工作）。

> ⚠️ 建议使用 VS Code Live Server 以获得最佳体验。

---

### 📖 使用指南

#### 用户注册

1. 打开登录页面，点击「注册新账号」
2. 输入用户名（2-50字符，支持中文、字母、数字、下划线）
3. 输入密码（至少6个字符），可以看到密码强度指示器
4. 确认密码，点击「注册」
5. 注册成功自动跳转到主面板

#### 获取内容

1. 主面板顶部有两个 Tab：**😄 随机笑话** 和 **💪 鸡汤文案**
2. 点击「换一个」按钮获取新的内容
3. 内容卡片带有翻转动画效果

#### 收藏与历史

| 功能 | 操作 | 说明 |
|------|------|------|
| ❤️ 收藏 | 点击内容下方的 ❤️ 收藏 按钮 | 收藏当前展示的内容 |
| ⭐ 查看收藏 | 点击右下角的 ⭐ 浮动按钮 | 打开侧边抽屉查看所有收藏 |
| 🗑 取消收藏 | 在收藏列表中悬停项，点击 🗑 按钮 | 删除指定收藏 |
| 🕐 查看历史 | 在侧边抽屉中切换到「历史」Tab | 查看浏览过的所有内容 |
| 🗑 清空历史 | 在历史列表底部点击「清空浏览历史」| 删除所有历史记录 |

#### 其他操作

- **📋 复制** — 一键复制文案到剪贴板
- **退出登录** — 点击右上角的「退出」按钮

---

### 🔗 第三方 API 说明

Web 版接入以下内容来源。

| API | 用途 | 接入位置 |
|-----|------|----------|
| **韩小韩 API** | 中文随机笑话 | [笑话服务](backend/services/joke_service.py) |
| **一言 Hitokoto** | 中文名言 | [名言服务](backend/services/quote_service.py) |

> 当所有外部 API 都不可用时，系统会自动使用内置的本地备选数据，确保应用不中断。

---

### 🛠 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| **后端框架** | FastAPI 0.115 | 高性能异步 Web 框架 |
| **ORM** | SQLAlchemy 2.0 | Python 最流行的 ORM |
| **数据库** | SQLite | 零配置，文件级数据库 |
| **认证** | JWT + bcrypt | 安全的令牌认证 + 密码加密 |
| **前端** | 原生 HTML/CSS/JS | 零框架依赖，轻量快速 |
| **样式** | 毛玻璃 (Glassmorphism) | 现代简洁的 UI 风格 |
| **HTTP 客户端** | httpx | 异步调用第三方 API |
| **服务器** | uvicorn | 高性能 ASGI 服务器 |

---

### 📝 API 一览

| 方法 | 路径 | 说明 | 需认证 |
|------|------|------|--------|
| POST | `/api/v1/users/register` | 用户注册 | ❌ |
| POST | `/api/v1/users/login` | 用户登录 | ❌ |
| GET | `/api/v1/users/me` | 获取当前用户信息 | ✅ |
| GET | `/api/v1/content/joke` | 获取随机笑话 | ✅ |
| GET | `/api/v1/content/quote` | 获取随机鸡汤 | ✅ |
| GET | `/api/v1/content/history` | 获取浏览历史 | ✅ |
| DELETE | `/api/v1/content/history` | 清空浏览历史 | ✅ |
| POST | `/api/v1/favorites/` | 添加收藏 | ✅ |
| GET | `/api/v1/favorites/` | 查看收藏列表 | ✅ |
| DELETE | `/api/v1/favorites/{id}` | 删除收藏 | ✅ |

详细接口文档请查看 [API.md](API.md) 或启动后端后访问 http://localhost:8000/docs。

---

### 🌟 功能特点

- ✅ **纯前后端分离** — FastAPI + 原生前端，架构清晰
- ✅ **统一响应格式** — 所有接口返回 `{ code, message, data }` 结构
- ✅ **JWT 认证** — 安全的令牌机制，数据完全隔离
- ✅ **离线回退** — 第三方 API 不可用时使用内置内容
- ✅ **密码安全** — bcrypt 哈希存储，绝不保存明文密码
- ✅ **毛玻璃 UI** — 现代简约的 Glassmorphism 设计风格
- ✅ **响应式布局** — 适配桌面端和移动端
- ✅ **交互反馈** — 加载状态、成功/错误提示、动画过渡
- ✅ **全面错误处理** — 前后端双重校验，异常信息友好

---

## 📄 License

MIT License © 2026 WenMind Studio
