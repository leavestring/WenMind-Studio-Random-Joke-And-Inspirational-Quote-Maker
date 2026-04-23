"""
WenMind Studio - Pydantic 数据模型（Schemas）
定义 API 请求和响应的数据结构，自动校验和序列化
"""

from datetime import datetime
from typing import Optional

from pydantic import BaseModel, Field, field_validator


# ============================================================
# 统一响应格式
# ============================================================

class ApiResponse(BaseModel):
    """
    所有 API 的统一响应格式
    code: 业务状态码（200=成功，4xxx=客户端错误，5xxx=服务端错误）
    message: 提示信息
    data: 实际数据负载
    """
    code: int = Field(default=200, description="业务状态码")
    message: str = Field(default="success", description="提示信息")
    data: Optional[dict | list] = Field(default=None, description="数据负载")


# ============================================================
# 用户相关
# ============================================================

class UserRegisterRequest(BaseModel):
    """用户注册请求体"""
    username: str = Field(..., min_length=2, max_length=50, description="用户名，2-50个字符")
    password: str = Field(..., min_length=6, max_length=50, description="密码，6-50个字符")
    confirm_password: str = Field(..., description="确认密码，需与 password 一致")

    @field_validator("username")
    @classmethod
    def username_alphanumeric(cls, v: str) -> str:
        """用户名只能包含字母、数字、下划线和中文"""
        import re
        if not re.match(r'^[\u4e00-\u9fa5a-zA-Z0-9_]+$', v):
            raise ValueError("用户名只能包含中文、字母、数字和下划线")
        return v.strip()

    @field_validator("confirm_password")
    @classmethod
    def passwords_match(cls, v: str, info) -> str:
        """验证两次密码输入一致"""
        if "password" in info.data and v != info.data["password"]:
            raise ValueError("两次输入的密码不一致")
        return v


class UserLoginRequest(BaseModel):
    """用户登录请求体"""
    username: str = Field(..., description="用户名")
    password: str = Field(..., description="密码")


class UserResponse(BaseModel):
    """用户信息响应（不包含密码）"""
    id: int
    username: str
    nickname: str
    created_at: datetime

    class Config:
        from_attributes = True


class TokenResponse(BaseModel):
    """登录成功后的令牌响应"""
    access_token: str = Field(..., description="JWT 访问令牌")
    token_type: str = Field(default="bearer", description="令牌类型")
    user: UserResponse


# ============================================================
# 内容相关
# ============================================================

class ContentResponse(BaseModel):
    """单条内容响应（笑话或鸡汤）"""
    id: str = Field(..., description="内容唯一标识")
    content: str = Field(..., description="文案内容")
    author: str = Field(default="", description="作者")
    category: str = Field(..., description="类型: joke=笑话, quote=鸡汤")
    source: str = Field(default="", description="来源 API")


class FavoriteCreateRequest(BaseModel):
    """收藏创建请求体"""
    content: str = Field(..., description="要收藏的文案内容")
    author: str = Field(default="", description="作者")
    category: str = Field(..., pattern="^(joke|quote)$", description="类型: joke 或 quote")
    source: str = Field(default="", description="来源")


class FavoriteResponse(BaseModel):
    """收藏记录响应"""
    id: int
    content: str
    author: str
    category: str
    source: str
    created_at: datetime

    class Config:
        from_attributes = True


class HistoryResponse(BaseModel):
    """浏览历史响应"""
    id: int
    content: str
    author: str
    category: str
    source: str
    viewed_at: datetime

    class Config:
        from_attributes = True
