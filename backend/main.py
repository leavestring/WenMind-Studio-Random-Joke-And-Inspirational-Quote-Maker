"""
WenMind Studio - FastAPI 应用入口
随机笑话与鸡汤文案生成收藏系统后端

启动命令:
    uvicorn backend.main:app --reload --host 0.0.0.0 --port 8000

访问 API 文档:
    http://localhost:8000/docs
"""

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from contextlib import asynccontextmanager

from backend.database import init_db
from backend.utils.exceptions import global_exception_handler
from backend.routers import users, content, favorites

# ============================================================
# 应用生命周期管理
# ============================================================

@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    应用启动/关闭时的生命周期钩子

    - 启动时：初始化数据库表结构
    - 关闭时：清理资源（如需）
    """
    # 启动：初始化数据库
    init_db()
    print("[WenMind] Database initialized successfully")
    print("[WenMind] WenMind Studio API started")
    yield
    # 关闭：可在此处添加清理逻辑
    print("[WenMind] Application shutdown")


# ============================================================
# 创建 FastAPI 应用实例
# ============================================================

app = FastAPI(
    title="WenMind Studio API",
    description="随机笑话与鸡汤文案生成收藏系统 - 后端接口服务\n\n"
    "功能:\n"
    "- 用户注册与登录（JWT 认证）\n"
    "- 随机获取笑话（接入 JokeAPI / Official Joke API）\n"
    "- 随机获取鸡汤文案（接入 ZenQuotes / Quotable）\n"
    "- 收藏喜欢的文案\n"
    "- 查看浏览历史",
    version="1.0.0",
    lifespan=lifespan,
    docs_url="/docs",          # Swagger UI 文档地址
    redoc_url="/redoc",        # ReDoc 文档地址
)

# ============================================================
# 跨域配置（CORS）
# 允许前端开发服务器和静态文件访问
# ============================================================

app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:5500",       # VS Code Live Server
        "http://localhost:3000",       # 开发服务器
        "http://127.0.0.1:5500",
        "http://127.0.0.1:3000",
        "null",                        # 允许 file:// 协议打开的前端
    ],
    allow_credentials=True,
    allow_methods=["*"],               # 允许所有 HTTP 方法
    allow_headers=["*"],               # 允许所有请求头
    expose_headers=["*"],
    max_age=3600,
)

# ============================================================
# 注册异常处理器（按具体异常类型注册，避免捕获 Exception 基类）
# 防止 Starlette 的 ServerErrorMiddleware 重复记录错误日志
# ============================================================

from backend.utils.exceptions import AppException
from starlette.exceptions import HTTPException as StarletteHTTPException
from fastapi.exceptions import RequestValidationError

app.add_exception_handler(AppException, global_exception_handler)
app.add_exception_handler(StarletteHTTPException, global_exception_handler)
app.add_exception_handler(RequestValidationError, global_exception_handler)

# ============================================================
# 注册路由
# ============================================================

app.include_router(users.router)        # /api/v1/users/*
app.include_router(content.router)      # /api/v1/content/*
app.include_router(favorites.router)    # /api/v1/favorites/*


# ============================================================
# 根路径路由 - API 状态检查
# ============================================================

@app.get("/")
async def root():
    """API 根路径，返回服务状态信息"""
    return {
        "name": "WenMind Studio API",
        "version": "1.0.0",
        "status": "running",
        "docs": "/docs",
    }


@app.get("/health")
async def health_check():
    """健康检查接口，用于监控服务状态"""
    return {
        "status": "healthy",
        "service": "WenMind Studio API",
    }
