"""
WenMind Studio - 自定义异常处理
定义业务异常类及全局异常捕获处理器
"""

from fastapi import Request
from fastapi.exceptions import RequestValidationError
from starlette.exceptions import HTTPException as StarletteHTTPException

from backend.utils.response import error, ErrorCodes


class AppException(Exception):
    """
    自定义业务异常基类

    Attributes:
        message: 错误提示信息
        code: 业务错误码
        http_status_code: HTTP 状态码
    """

    def __init__(
        self,
        message: str = "服务器内部错误",
        code: int = 50000,
        http_status_code: int = 500,
    ):
        self.message = message
        self.code = code
        self.http_status_code = http_status_code


class NotFoundException(AppException):
    """资源不存在异常（404）"""
    def __init__(self, message: str = "请求的资源不存在"):
        super().__init__(
            message=message,
            code=ErrorCodes.NOT_FOUND,
            http_status_code=404,
        )


class UnauthorizedException(AppException):
    """未认证异常（401）"""
    def __init__(self, message: str = "请先登录"):
        super().__init__(
            message=message,
            code=ErrorCodes.UNAUTHORIZED,
            http_status_code=401,
        )


class TokenExpiredException(AppException):
    """令牌过期异常（401）"""
    def __init__(self, message: str = "登录已过期，请重新登录"):
        super().__init__(
            message=message,
            code=ErrorCodes.TOKEN_EXPIRED,
            http_status_code=401,
        )


class ForbiddenException(AppException):
    """无权限异常（403）"""
    def __init__(self, message: str = "无权访问此资源"):
        super().__init__(
            message=message,
            code=ErrorCodes.FORBIDDEN,
            http_status_code=403,
        )


class DuplicateException(AppException):
    """资源重复异常（409）"""
    def __init__(self, message: str = "资源已存在", code: int = 40900):
        super().__init__(
            message=message,
            code=code,
            http_status_code=409,
        )


class ServiceException(AppException):
    """第三方服务调用异常（502）"""
    def __init__(self, message: str = "外部服务暂时不可用"):
        super().__init__(
            message=message,
            code=ErrorCodes.API_SERVICE_ERROR,
            http_status_code=502,
        )


# ============================================================
# 全局异常处理器
# ============================================================

async def global_exception_handler(request: Request, exc: Exception):
    """
    全局异常捕获入口
    所有未被捕获的异常最终会进入此处理器
    """
    if isinstance(exc, AppException):
        # 自定义业务异常
        return error(
            message=exc.message,
            code=exc.code,
            http_status_code=exc.http_status_code,
        )

    if isinstance(exc, StarletteHTTPException):
        # FastAPI/Starlette HTTP 异常（如 404、405 等）
        return error(
            message=exc.detail,
            code=exc.status_code * 100,  # 如 40400
            http_status_code=exc.status_code,
        )

    if isinstance(exc, RequestValidationError):
        # 请求参数校验失败
        errors = exc.errors()
        first_error = errors[0] if errors else {}
        msg = first_error.get("msg", "请求参数格式错误")

        # 提取更友好的错误信息
        loc = first_error.get("loc", [])
        if len(loc) >= 2:
            field_name = loc[-1]
            msg = f"字段 '{field_name}': {msg}"

        return error(
            message=msg,
            code=ErrorCodes.BAD_REQUEST,
            http_status_code=422,
        )

    # 未知异常：返回 500
    return error(
        message="服务器内部错误",
        code=50000,
        http_status_code=500,
    )
