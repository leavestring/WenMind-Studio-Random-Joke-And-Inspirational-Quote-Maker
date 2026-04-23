"""
WenMind Studio - 数据库配置模块
SQLite 数据库连接与会话管理
"""

from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker, declarative_base

# SQLite 数据库文件路径（位于 backend 目录下）
DATABASE_URL = "sqlite:///./wenmind.db"

# 创建数据库引擎
# connect_args={"check_same_thread": False} 是 SQLite 多线程访问的必要配置
engine = create_engine(
    DATABASE_URL,
    connect_args={"check_same_thread": False},  # 允许跨线程使用同一个连接
    echo=False,                                  # 生产环境建议关闭 SQL 日志
)

# 会话工厂：用于创建数据库会话实例
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)

# ORM 模型基类：所有数据库模型都要继承此类
Base = declarative_base()


def init_db():
    """
    初始化数据库：创建所有定义的表结构
    在应用启动时调用一次即可
    """
    Base.metadata.create_all(bind=engine)


def get_db():
    """
    数据库会话依赖注入函数
    使用 FastAPI 的 Depends 机制自动管理会话生命周期
    请求结束时自动关闭会话，释放连接

    用法:
        @app.get("/items")
        def get_items(db: Session = Depends(get_db)):
            ...
    """
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
