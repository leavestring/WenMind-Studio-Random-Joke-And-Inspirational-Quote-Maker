"""
WenMind Studio - JWT 用户认证模块
处理用户密码哈希、JWT 令牌生成与验证

使用 bcrypt 直接进行密码哈希（替代 passlib，解决兼容性问题）
"""

from datetime import datetime, timedelta
from typing import Optional

import bcrypt
from fastapi import Depends, Request
from jose import JWTError, jwt
from sqlalchemy.orm import Session

from backend.database import get_db
from backend.models import User
from backend.utils.exceptions import UnauthorizedException, TokenExpiredException

# ============================================================
# 配置
# ============================================================

# JWT 密钥（生产环境应改为环境变量或配置文件）
SECRET_KEY = "wenmind-studio-secret-key-change-in-production"
ALGORITHM = "HS256"
ACCESS_TOKEN_EXPIRE_MINUTES = 60 * 24 * 7  # 7 天过期


# ============================================================
# 密码工具函数（直接使用 bcrypt）
# ============================================================

def hash_password(password: str) -> str:
    """
    对明文密码进行 bcrypt 哈希

    Args:
        password: 明文密码

    Returns:
        str: 哈希后的密码字符串
    """
    # bcrypt 要求密码为 bytes 类型
    password_bytes = password.encode("utf-8")
    # 自动生成 salt 并哈希
    hashed = bcrypt.hashpw(password_bytes, bcrypt.gensalt())
    return hashed.decode("utf-8")


def verify_password(plain_password: str, hashed_password: str) -> bool:
    """
    验证明文密码是否与哈希值匹配

    Args:
        plain_password: 明文密码
        hashed_password: 哈希后的密码

    Returns:
        bool: 匹配返回 True，否则 False
    """
    try:
        return bcrypt.checkpw(
            plain_password.encode("utf-8"),
            hashed_password.encode("utf-8"),
        )
    except Exception:
        return False


# ============================================================
# JWT 令牌工具函数
# ============================================================

def create_access_token(data: dict, expires_delta: Optional[timedelta] = None) -> str:
    """
    创建 JWT 访问令牌

    Args:
        data: 要编码到令牌中的数据（通常包含 user_id）
        expires_delta: 过期时间增量，默认使用 ACCESS_TOKEN_EXPIRE_MINUTES

    Returns:
        str: 编码后的 JWT 字符串
    """
    to_encode = data.copy()
    expire = datetime.utcnow() + (expires_delta or timedelta(minutes=ACCESS_TOKEN_EXPIRE_MINUTES))
    to_encode.update({"exp": expire})
    return jwt.encode(to_encode, SECRET_KEY, algorithm=ALGORITHM)


def decode_access_token(token: str) -> dict:
    """
    解码并验证 JWT 令牌

    Args:
        token: JWT 字符串

    Returns:
        dict: 令牌中解码出的数据

    Raises:
        TokenExpiredException: 令牌已过期
        UnauthorizedException: 令牌无效
    """
    try:
        payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
        return payload
    except jwt.ExpiredSignatureError:
        raise TokenExpiredException("登录已过期，请重新登录")
    except JWTError:
        raise UnauthorizedException("无效的访问令牌")


# ============================================================
# 依赖注入：获取当前登录用户
# ============================================================

async def get_current_user(
    request: Request,
    db: Session = Depends(get_db),
) -> User:
    """
    从请求头中提取 JWT 令牌，验证并返回当前用户对象
    作为 FastAPI 依赖注入使用

    用法:
        @app.get("/profile")
        def get_profile(current_user: User = Depends(get_current_user)):
            ...

    Args:
        request: FastAPI 请求对象
        db: 数据库会话

    Returns:
        User: 当前认证用户对象

    Raises:
        UnauthorizedException: 未提供或提供了无效的令牌
    """
    # 从 Authorization 头提取 Bearer token
    auth_header = request.headers.get("Authorization", "")
    if not auth_header.startswith("Bearer "):
        raise UnauthorizedException("请先登录")

    token = auth_header.replace("Bearer ", "")
    payload = decode_access_token(token)

    user_id = payload.get("user_id")
    if user_id is None:
        raise UnauthorizedException("令牌无效")

    user = db.query(User).filter(User.id == user_id).first()
    if user is None:
        raise UnauthorizedException("用户不存在")

    return user
