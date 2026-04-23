"""
WenMind Studio - 鸡汤文案内容服务
优先从中文名言/鸡汤 API 获取内容，API 不可用时使用本地中文备选数据
"""

import hashlib
import random
from typing import Optional

import httpx

from backend.utils.exceptions import ServiceException

# ============================================================
# 第三方鸡汤/名言 API 配置
# ============================================================

# 一言（Hitokoto）— 中文句子 API
# https://developer.hitokoto.cn/
# 免费，无需 API Key
# 返回格式: {"id": xxx, "hitokoto": "句子内容", "from": "来源", "from_who": "作者"}
HITOKOTO_API_URL = "https://v1.hitokoto.cn/"

# 天行数据 — 一言/名言（需要 API Key，暂时不用）

# 本地备选文案（当所有外部 API 都不可用时）
FALLBACK_QUOTES = [
    {"content": "生活就像一盒巧克力，你永远不知道下一颗是什么味道。", "author": "阿甘正传"},
    {"content": "不要问你的国家能为你做什么，而要问你能为你的国家做什么。", "author": "约翰·F·肯尼迪"},
    {"content": "千里之行，始于足下。", "author": "老子"},
    {"content": "世界上只有一种真正的英雄主义，那就是认清生活的真相后依然热爱生活。", "author": "罗曼·罗兰"},
    {"content": "路漫漫其修远兮，吾将上下而求索。", "author": "屈原"},
    {"content": "天行健，君子以自强不息。", "author": "周易"},
    {"content": "人生没有白走的路，每一步都算数。", "author": ""},
    {"content": "所谓成长，就是把哭声调成静音的过程。", "author": ""},
    {"content": "你现在的努力，是为了以后能有更多选择的权利。", "author": ""},
    {"content": "别让任何人告诉你，你做不到什么事。如果你有梦想，就要去捍卫它。", "author": "当幸福来敲门"},
    {"content": "种一棵树最好的时间是十年前，其次是现在。", "author": ""},
    {"content": "你的负担将变成礼物，你受的苦将照亮你的路。", "author": "泰戈尔"},
    {"content": "人生如逆旅，我亦是行人。", "author": "苏轼"},
    {"content": "世上无难事，只怕有心人。", "author": ""},
    {"content": "星光不问赶路人，时光不负有心人。", "author": ""},
    {"content": "愿你历尽千帆，归来仍是少年。", "author": ""},
    {"content": "你若盛开，蝴蝶自来；你若精彩，天自安排。", "author": ""},
    {"content": "生活不止眼前的苟且，还有诗和远方的田野。", "author": "高晓松"},
    {"content": "不要因为走得太远，忘了我们为什么出发。", "author": "纪伯伦"},
    {"content": "每一个不曾起舞的日子，都是对生命的辜负。", "author": "尼采"},
    {"content": "黑夜给了我黑色的眼睛，我却用它寻找光明。", "author": "顾城"},
    {"content": "面朝大海，春暖花开。", "author": "海子"},
    {"content": "天生我材必有用，千金散尽还复来。", "author": "李白"},
    {"content": "竹杖芒鞋轻胜马，谁怕？一蓑烟雨任平生。", "author": "苏轼"},
    {"content": "长风破浪会有时，直挂云帆济沧海。", "author": "李白"},
]


async def fetch_random_quote() -> dict:
    """
    从多个中文 API 获取随机鸡汤/名言

    Returns:
        dict: 包含文案内容和作者，格式: {"content": str, "author": str}

    Raises:
        ServiceException: 所有 API 调用失败时抛出
    """
    # 尝试从一言（Hitokoto）获取
    try:
        async with httpx.AsyncClient(timeout=10) as client:
            response = await client.get(
                HITOKOTO_API_URL,
                params={"c": "d", "encode": "json"},  # c=d 表示哲理分类
            )
            if response.status_code == 200:
                data = response.json()
                content = data.get("hitokoto", "")
                author = data.get("from_who") or data.get("from", "")
                if content:
                    return {
                        "content": content,
                        "author": author,
                        "source": "Hitokoto",
                    }
    except Exception as e:
        print(f"[QuoteService] 一言 API 调用失败: {e}")

    # 所有外部 API 都失败时，使用本地备选数据
    print("[QuoteService] 使用本地备选鸡汤文案")
    quote = random.choice(FALLBACK_QUOTES)
    return {
        "content": quote["content"],
        "author": quote["author"],
        "source": "local_fallback",
    }


def generate_content_id(content: str) -> str:
    """根据内容生成唯一 ID"""
    return hashlib.md5(content.encode("utf-8")).hexdigest()[:16]
