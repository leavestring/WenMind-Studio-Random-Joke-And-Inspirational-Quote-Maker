"""
WenMind Studio - 内容获取路由模块
提供随机笑话和鸡汤文案的获取接口
"""

from fastapi import APIRouter, Depends, Query
from sqlalchemy.orm import Session

from backend.database import get_db
from backend.models import User, ViewHistory
from backend.auth import get_current_user
from backend.schemas import ContentResponse
from backend.services.joke_service import fetch_random_joke, generate_content_id as joke_id
from backend.services.quote_service import fetch_random_quote, generate_content_id as quote_id
from backend.utils.response import success, error, ErrorCodes

# 创建路由实例
router = APIRouter(prefix="/api/v1/content", tags=["内容获取"])


@router.get("/joke")
async def get_random_joke(
    current_user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
):
    """
    获取随机笑话（需认证）

    从第三方公开免费 API 获取随机笑话，
    若 API 不可用则使用本地备选数据。

    返回:
        code=200: 成功返回笑话内容
        code=42002: 外部服务不可用
    """
    try:
        joke = await fetch_random_joke()
    except Exception as e:
        return error(
            message="暂时获取不到笑话，请稍后再试",
            code=ErrorCodes.API_SERVICE_ERROR,
        )

    content_id = joke_id(joke["content"])

    # 记录浏览历史
    try:
        history = ViewHistory(
            user_id=current_user.id,
            content=joke["content"],
            author=joke.get("author", ""),
            category="joke",
            source=joke.get("source", "unknown"),
        )
        db.add(history)
        db.commit()
    except Exception:
        db.rollback()
        # 历史记录失败不影响主流程

    return success(
        data=ContentResponse(
            id=content_id,
            content=joke["content"],
            author=joke.get("author", ""),
            category="joke",
            source=joke.get("source", "unknown"),
        ).model_dump(),
    )


@router.get("/quote")
async def get_random_quote(
    current_user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
):
    """
    获取随机鸡汤文案（需认证）

    从多个第三方公开免费 API 获取随机治愈/励志文案，
    若所有 API 都不可用则使用本地备选数据。

    返回:
        code=200: 成功返回鸡汤文案
        code=42002: 外部服务不可用
    """
    try:
        quote = await fetch_random_quote()
    except Exception as e:
        return error(
            message="暂时获取不到文案，请稍后再试",
            code=ErrorCodes.API_SERVICE_ERROR,
        )

    content_id = quote_id(quote["content"])

    # 记录浏览历史
    try:
        history = ViewHistory(
            user_id=current_user.id,
            content=quote["content"],
            author=quote.get("author", ""),
            category="quote",
            source=quote.get("source", "unknown"),
        )
        db.add(history)
        db.commit()
    except Exception:
        db.rollback()

    return success(
        data=ContentResponse(
            id=content_id,
            content=quote["content"],
            author=quote.get("author", ""),
            category="quote",
            source=quote.get("source", "unknown"),
        ).model_dump(),
    )


@router.get("/history")
async def get_view_history(
    current_user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
    limit: int = Query(default=50, ge=1, le=200, description="返回条数"),
):
    """
    获取当前用户的浏览历史（需认证）

    查询参数:
        limit: 返回的历史记录条数，默认50，最多200

    返回:
        code=200: 历史记录列表，按时间倒序
    """
    history_records = (
        db.query(ViewHistory)
        .filter(ViewHistory.user_id == current_user.id)
        .order_by(ViewHistory.viewed_at.desc())
        .limit(limit)
        .all()
    )

    return success(
        data=[
            {
                "id": h.id,
                "content": h.content,
                "author": h.author,
                "category": h.category,
                "source": h.source,
                "viewed_at": h.viewed_at.isoformat(),
            }
            for h in history_records
        ],
    )


@router.delete("/history")
async def clear_view_history(
    current_user: User = Depends(get_current_user),
    db: Session = Depends(get_db),
):
    """
    清空当前用户的浏览历史（需认证）

    返回:
        code=200: 清空成功
    """
    try:
        db.query(ViewHistory).filter(ViewHistory.user_id == current_user.id).delete()
        db.commit()
    except Exception as e:
        db.rollback()
        return error(message=f"清空历史失败: {str(e)}")

    return success(message="浏览历史已清空")
