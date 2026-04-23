"""
WenMind Studio - 收藏管理路由模块
处理用户收藏的添加、查看、删除等操作
"""

from typing import Optional

from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session

from backend.database import get_db
from backend.models import User, Favorite
from backend.auth import get_current_user
from backend.schemas import FavoriteCreateRequest, FavoriteResponse
from backend.utils.response import success, error, ErrorCodes

# 创建路由实例
router = APIRouter(prefix="/api/v1/favorites", tags=["收藏管理"])


@router.post("/")
async def add_favorite(
    req: FavoriteCreateRequest,
    current_user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
):
    """
    添加收藏（需认证）

    请求体:
        content: 文案内容
        author: 作者（可选）
        category: 类型(joke/quote)
        source: 来源（可选）

    返回:
        code=200: 收藏成功
        code=42003: 已收藏过相同内容
    """
    # 检查是否已收藏过相同内容
    existing = (
        db.query(Favorite)
        .filter(
            Favorite.user_id == current_user.id,
            Favorite.content == req.content,
        )
        .first()
    )
    if existing:
        return error(
            message="该内容已收藏过",
            code=ErrorCodes.FAVORITE_EXISTS,
        )

    # 创建收藏记录
    try:
        fav = Favorite(
            user_id=current_user.id,
            content=req.content,
            author=req.author,
            category=req.category,
            source=req.source,
        )
        db.add(fav)
        db.commit()
        db.refresh(fav)
    except Exception as e:
        db.rollback()
        return error(message=f"收藏失败: {str(e)}")

    return success(
        data=FavoriteResponse(
            id=fav.id,
            content=fav.content,
            author=fav.author,
            category=fav.category,
            source=fav.source,
            created_at=fav.created_at,
        ).model_dump(mode="json"),
        message="收藏成功",
    )


@router.get("/")
async def list_favorites(
    current_user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
    category: Optional[str] = Query(default=None, pattern="^(joke|quote)?$", description="筛选类型"),
    limit: int = Query(default=50, ge=1, le=200, description="返回条数"),
):
    """
    查看收藏列表（需认证）

    查询参数:
        category: 可选，按类型筛选(joke/quote)，不传则返回全部
        limit: 返回条数，默认50，最多200

    返回:
        code=200: 收藏列表，按收藏时间倒序
    """
    query = db.query(Favorite).filter(Favorite.user_id == current_user.id)

    if category:
        query = query.filter(Favorite.category == category)

    favorites = query.order_by(Favorite.created_at.desc()).limit(limit).all()

    return success(
        data=[
            FavoriteResponse(
                id=f.id,
                content=f.content,
                author=f.author,
                category=f.category,
                source=f.source,
                created_at=f.created_at,
            ).model_dump(mode="json")
            for f in favorites
        ],
    )


@router.delete("/{favorite_id}")
async def delete_favorite(
    favorite_id: int,
    current_user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
):
    """
    删除指定收藏（需认证）

    路径参数:
        favorite_id: 收藏记录的 ID

    返回:
        code=200: 删除成功
        code=42004: 收藏记录不存在
    """
    fav = db.query(Favorite).filter(Favorite.id == favorite_id).first()
    if not fav:
        return error(
            message="收藏记录不存在",
            code=ErrorCodes.FAVORITE_NOT_FOUND,
        )

    # 确保只能删除自己的收藏
    if fav.user_id != current_user.id:
        return error(
            message="无权删除此收藏",
            code=ErrorCodes.FORBIDDEN,
        )

    try:
        db.delete(fav)
        db.commit()
    except Exception as e:
        db.rollback()
        return error(message=f"删除失败: {str(e)}")

    return success(message="已取消收藏")
