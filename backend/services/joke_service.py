"""
WenMind Studio - 笑话内容服务
优先从中文笑话 API 获取内容，API 不可用时使用本地中文备选数据
"""

import hashlib
import random
from typing import Optional

import httpx

from backend.utils.exceptions import ServiceException

# ============================================================
# 第三方笑话 API 配置
# ============================================================

# 韩小韩 API 随机笑话（中文）
# https://api.vvhan.com/api/xh?type=json
# 返回格式: {"code": 200, "data": {"content": "笑话内容"}}
HAN_JOKE_API_URL = "https://api.vvhan.com/api/xh"

# 备用中文笑话 API — 天行数据
# https://www.tianapi.com/ 需要 key，暂时不用

# 本地备选笑话（当所有外部 API 都不可用时）
FALLBACK_JOKES = [
    {"content": "为什么程序员总分不清万圣节和圣诞节？因为 Oct 31 等于 Dec 25。", "author": ""},
    {"content": "有一天，小明去面试，面试官说：'你最大的缺点是什么？' 小明说：'诚实。' 面试官说：'我不觉得诚实是缺点。' 小明说：'我他妈才不在乎你怎么想。'", "author": ""},
    {"content": "甲：'我昨晚梦到我去微软上班了。' 乙：'然后呢？' 甲：'然后系统就崩溃了。'", "author": ""},
    {"content": "有一天，汉堡包走在路上，突然感到很不舒服。旁边的热狗问：'你怎么了？' 汉堡包说：'我感觉我里面有个人。'", "author": ""},
    {"content": "为什么数学书总是很忧伤？因为它有太多的问题。", "author": ""},
    {"content": "我决定把「多喝热水」改成「多喝岩浆」，这样就不会有人来烦我了。", "author": ""},
    {"content": "今天去银行取钱，柜台小姐姐说：'您好，请问办理什么业务？' 我说：'我想取点钱。' 她说：'请问您卡里有钱吗？' 我说：'有，不然我来干嘛。' 她说：'那请您先去柜台对面的ATM机取。' 我：'......'", "author": ""},
    {"content": "一条狗走进一家网吧，对老板说：'老板，能上网吗？' 老板说：'能啊，你要上什么网？' 狗说：'我想上汪汪网。'", "author": ""},
    {"content": "面试官：'你有什么特长？' 我：'我的手指特别长。' 面试官：'这算什么特长？' 我：'我可以同时按到Ctrl+Alt+Del。'", "author": ""},
    {"content": "为什么程序员总是混淆万圣节和圣诞节？因为 Oct 31 == Dec 25。", "author": ""},
    {"content": "老师：'用「左右为难」造句。' 学生：'我考试时，左右为难。' 老师：'为什么？' 学生：'左边同学让我抄他的，右边同学也让我抄他的，我不知道抄谁的。'", "author": ""},
    {"content": "去朋友家玩，看见他家的狗在门口站着，我就问：'你家的狗怎么不叫啊？' 朋友说：'它现在没空，正在看家。'", "author": ""},
    {"content": "小明：'我爸爸是警察，我妈妈是老师。' 小红：'那你呢？' 小明：'我是他们爱情的结晶。' 小红：'结晶？我看你是他们的结石吧。'", "author": ""},
    {"content": "什么叫「万死不辞」？就是每天被气得要死，但就是不想辞职。", "author": ""},
    {"content": "今天我决定减肥，闺蜜发来一条消息：'加油，你是最胖的！' 我：'......'", "author": ""},
    {"content": "去食堂打饭，阿姨说：'同学你要什么？' 我说：'我要一份西红柿炒鸡蛋。' 阿姨说：'对不起，今天没有鸡蛋。' 我说：'那西红柿炒什么？' 阿姨说：'西红柿炒西红柿。'", "author": ""},
    {"content": "有个人去钓鱼，钓了半天一条也没钓到。旁边的人说：'你换个地方试试？' 他说：'不用，我感觉这里鱼多。' 旁边的人问：'你怎么知道？' 他说：'因为水面上全是鱼漂。'", "author": ""},
    {"content": "老公：'老婆，我爱你。' 老婆：'你是不是又想要零花钱了？' 老公：'不，我是真心话。' 老婆：'那更可怕，你是不是在外面做坏事了？'", "author": ""},
    {"content": "为什么蚊子只叮我？因为我是O型血吗？不，因为你是「甜」血。", "author": ""},
    {"content": "有一天，0和8在街上相遇。0看了8一眼说：'哇，好身材！系腰带了吧？'", "author": ""},
]


async def fetch_random_joke() -> dict:
    """
    从中文笑话 API 获取随机笑话

    Returns:
        dict: 包含笑话内容，格式: {"content": str, "author": str}

    Raises:
        ServiceException: 所有 API 调用失败时抛出
    """
    # 尝试从中文 API 获取
    try:
        async with httpx.AsyncClient(timeout=10) as client:
            response = await client.get(
                HAN_JOKE_API_URL,
                params={"type": "json"},
            )
            if response.status_code == 200:
                data = response.json()
                # 韩小韩 API 返回: {"code": 200, "data": {"content": "..."}}
                if data.get("code") == 200 and "data" in data:
                    content = data["data"].get("content", "")
                    if content:
                        return {
                            "content": content,
                            "author": "",
                            "source": "HanJokeAPI",
                        }
    except Exception as e:
        print(f"[JokeService] 中文笑话 API 调用失败: {e}")

    # 所有外部 API 都失败时，使用本地备选数据
    print("[JokeService] 使用本地备选笑话")
    joke = random.choice(FALLBACK_JOKES)
    return {
        "content": joke["content"],
        "author": joke["author"],
        "source": "local_fallback",
    }


def generate_content_id(content: str) -> str:
    """根据内容生成唯一 ID"""
    return hashlib.md5(content.encode("utf-8")).hexdigest()[:16]
