"""
WenMind Studio - SQLAlchemy ORM 数据模型
定义数据库中所有表的结构
"""

from datetime import datetime

from sqlalchemy import Column, Integer, String, Text, DateTime, ForeignKey
from sqlalchemy.orm import relationship

from backend.database import Base


class User(Base):
    """用户表 - 存储注册用户信息"""
    __tablename__ = "users"

    id = Column(Integer, primary_key=True, index=True, autoincrement=True)
    username = Column(String(50), unique=True, nullable=False, index=True, comment="用户名，唯一")
    hashed_password = Column(String(255), nullable=False, comment="bcrypt 哈希后的密码")
    nickname = Column(String(50), default="", comment="用户昵称")
    created_at = Column(DateTime, default=datetime.utcnow, comment="注册时间")

    # 关联关系：一个用户有多条收藏记录
    favorites = relationship("Favorite", back_populates="user", cascade="all, delete-orphan")

    def __repr__(self):
        return f"<User(id={self.id}, username='{self.username}')>"


class Favorite(Base):
    """收藏表 - 用户收藏的笑话或鸡汤记录"""
    __tablename__ = "favorites"

    id = Column(Integer, primary_key=True, index=True, autoincrement=True)
    user_id = Column(Integer, ForeignKey("users.id", ondelete="CASCADE"), nullable=False, comment="用户 ID")
    content = Column(Text, nullable=False, comment="收藏的文案内容")
    author = Column(String(100), default="", comment="作者（鸡汤文案的作者）")
    category = Column(String(20), nullable=False, comment="类型: joke=笑话, quote=鸡汤")
    source = Column(String(100), default="", comment="来源（如 JokeAPI、ZenQuotes）")
    created_at = Column(DateTime, default=datetime.utcnow, comment="收藏时间")

    # 关联关系
    user = relationship("User", back_populates="favorites")

    def __repr__(self):
        return f"<Favorite(id={self.id}, user_id={self.user_id}, category='{self.category}')>"


class ViewHistory(Base):
    """浏览历史表 - 记录用户查看过的内容"""
    __tablename__ = "view_history"

    id = Column(Integer, primary_key=True, index=True, autoincrement=True)
    user_id = Column(Integer, ForeignKey("users.id", ondelete="CASCADE"), nullable=False, comment="用户 ID")
    content = Column(Text, nullable=False, comment="查看的文案内容")
    author = Column(String(100), default="", comment="作者")
    category = Column(String(20), nullable=False, comment="类型: joke=笑话, quote=鸡汤")
    source = Column(String(100), default="", comment="来源")
    viewed_at = Column(DateTime, default=datetime.utcnow, comment="查看时间")

    def __repr__(self):
        return f"<ViewHistory(id={self.id}, user_id={self.user_id}, category='{self.category}')>"
