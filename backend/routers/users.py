"""
WenMind Studio - 用户路由模块
处理用户注册、登录等认证相关接口
"""

from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from backend.database import get_db
from backend.models import User
from backend.schemas import (
    UserRegisterRequest,
    UserLoginRequest,
    UserResponse,
    TokenResponse,
)
from backend.auth import hash_password, verify_password, create_access_token, get_current_user
from backend.utils.response import success, error, ErrorCodes
from backend.utils.exceptions import DuplicateException, NotFoundException

# 创建路由实例，所有接口以 /api/v1/users 为前缀
router = APIRouter(prefix="/api/v1/users", tags=["用户管理"])


@router.post("/register")
async def register_user(req: UserRegisterRequest, db: Session = Depends(get_db)):
    """
    用户注册接口

    请求体:
        username: 用户名（2-50字符，仅支持中文、字母、数字、下划线）
        password: 密码（6-50字符）
        confirm_password: 确认密码

    返回:
        code=200: 注册成功
        code=41001: 用户名已存在
        code=40000: 参数校验失败
    """
    # 检查用户名是否已存在
    existing_user = db.query(User).filter(User.username == req.username).first()
    if existing_user:
        return error(
            message="该用户名已被注册",
            code=ErrorCodes.USERNAME_EXISTS,
        )

    # 创建新用户
    try:
        new_user = User(
            username=req.username,
            hashed_password=hash_password(req.password),
            nickname=req.username,  # 默认昵称与用户名相同
        )
        db.add(new_user)
        db.commit()
        db.refresh(new_user)
    except Exception as e:
        db.rollback()
        return error(message=f"注册失败: {str(e)}")

    # 生成 JWT 令牌
    token = create_access_token(data={"user_id": new_user.id})

    return success(
        data=TokenResponse(
            access_token=token,
            token_type="bearer",
            user=UserResponse(
                id=new_user.id,
                username=new_user.username,
                nickname=new_user.nickname,
                created_at=new_user.created_at,
            ),
        ).model_dump(mode="json"),
        message="注册成功",
    )


@router.post("/login")
async def login_user(req: UserLoginRequest, db: Session = Depends(get_db)):
    """
    用户登录接口

    请求体:
        username: 用户名
        password: 密码

    返回:
        code=200: 登录成功，返回 JWT 令牌
        code=41004: 用户不存在
        code=41003: 密码错误
    """
    # 查找用户
    user = db.query(User).filter(User.username == req.username).first()
    if not user:
        return error(
            message="用户名或密码错误",
            code=ErrorCodes.USER_NOT_FOUND,
        )

    # 验证密码
    if not verify_password(req.password, user.hashed_password):
        return error(
            message="用户名或密码错误",
            code=ErrorCodes.PASSWORD_WRONG,
        )

    # 生成 JWT 令牌
    token = create_access_token(data={"user_id": user.id})

    return success(
        data=TokenResponse(
            access_token=token,
            token_type="bearer",
            user=UserResponse(
                id=user.id,
                username=user.username,
                nickname=user.nickname,
                created_at=user.created_at,
            ),
        ).model_dump(mode="json"),
        message="登录成功",
    )


@router.get("/me")
async def get_current_user_info(current_user: User = Depends(get_current_user)):
    """
    获取当前登录用户信息（需认证）

    请求头:
        Authorization: Bearer <token>

    返回:
        code=200: 用户信息
        code=40100: 未登录或令牌无效
    """
    return success(
        data=UserResponse(
            id=current_user.id,
            username=current_user.username,
            nickname=current_user.nickname,
            created_at=current_user.created_at,
        ).model_dump(mode="json"),
    )
