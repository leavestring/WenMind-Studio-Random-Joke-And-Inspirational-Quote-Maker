"""
WenMind Studio - 统一 API 响应格式工具
确保所有接口返回一致的 JSON 结构
"""

from typing import Any, Optional

from fastapi import status
from fastapi.responses import JSONResponse


def success(data: Any = None, message: str = "success", code: int = 200) -> JSONResponse:
    """
    成功响应

    Args:
        data: 响应数据
        message: 提示信息
        code: 业务状态码

    Returns:
        JSONResponse: 统一格式的成功响应
    """
    return JSONResponse(
        status_code=status.HTTP_200_OK,
        content={
            "code": code,
            "message": message,
            "data": data,
        },
        headers={"Access-Control-Allow-Origin": "*"},
    )


def error(
    message: str = "请求失败",
    code: int = 40000,
    data: Any = None,
    http_status_code: int = status.HTTP_400_BAD_REQUEST,
) -> JSONResponse:
    """
    错误响应

    Args:
        message: 错误提示信息
        code: 业务错误码
        data: 可选的额外错误数据
        http_status_code: HTTP 状态码

    Returns:
        JSONResponse: 统一格式的错误响应（始终包含 CORS 头）
    """
    return JSONResponse(
        status_code=http_status_code,
        content={
            "code": code,
            "message": message,
            "data": data,
        },
        headers={"Access-Control-Allow-Origin": "*"},
    )


# ============================================================
# 预定义业务错误码
# ============================================================

class ErrorCodes:
    """业务错误码常量定义"""
    # 通用错误 (40xxx)
    BAD_REQUEST = 40000           # 请求参数错误
    UNAUTHORIZED = 40100          # 未认证
    TOKEN_EXPIRED = 40101         # 令牌过期
    TOKEN_INVALID = 40102         # 令牌无效
    FORBIDDEN = 40300             # 无权限
    NOT_FOUND = 40400             # 资源不存在

    # 用户相关错误 (41xxx)
    USERNAME_EXISTS = 41001       # 用户名已存在
    USERNAME_INVALID = 41002      # 用户名格式无效
    PASSWORD_WRONG = 41003        # 密码错误
    USER_NOT_FOUND = 41004        # 用户不存在
    PASSWORDS_NOT_MATCH = 41005   # 两次密码不一致

    # 内容相关错误 (42xxx)
    CONTENT_NOT_FOUND = 42001     # 内容不存在
    API_SERVICE_ERROR = 42002     # 第三方 API 调用失败
    FAVORITE_EXISTS = 42003       # 已收藏过
    FAVORITE_NOT_FOUND = 42004    # 收藏记录不存在
