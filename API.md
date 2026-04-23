# WenMind Studio API 接口文档

> 基础地址：`http://localhost:8000`
> Swagger 文档：`http://localhost:8000/docs`

---

## 📦 统一响应格式

所有 API 返回统一的 JSON 结构：

```json
{
    "code": 200,
    "message": "success",
    "data": { ... }
}
```

### 业务错误码

| 错误码 | 含义 |
|--------|------|
| `200` | 成功 |
| `40000` | 请求参数错误 |
| `40100` | 未认证（未登录） |
| `40101` | 令牌过期 |
| `40102` | 令牌无效 |
| `40300` | 无权限 |
| `40400` | 资源不存在 |
| `41001` | 用户名已存在 |
| `41003` | 密码错误 |
| `41004` | 用户不存在 |
| `42002` | 外部服务不可用 |
| `42003` | 已收藏过 |
| `42004` | 收藏记录不存在 |
| `50000` | 服务器内部错误 |

---

## 👤 用户模块

### POST `/api/v1/users/register`

用户注册。

**请求体：**

```json
{
    "username": "wenmind_user",
    "password": "123456",
    "confirm_password": "123456"
}
```

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "注册成功",
    "data": {
        "access_token": "eyJhbGciOiJIUzI1NiIs...",
        "token_type": "bearer",
        "user": {
            "id": 1,
            "username": "wenmind_user",
            "nickname": "wenmind_user",
            "created_at": "2026-04-23T12:00:00"
        }
    }
}
```

**错误响应：**

| 错误码 | 条件 |
|--------|------|
| `41001` | 用户名已存在 |
| `40000` | 用户名格式无效（非中文/字母/数字/下划线） |
| `40000` | 两次密码不一致 |

---

### POST `/api/v1/users/login`

用户登录。

**请求体：**

```json
{
    "username": "wenmind_user",
    "password": "123456"
}
```

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "登录成功",
    "data": {
        "access_token": "eyJhbGciOiJIUzI1NiIs...",
        "token_type": "bearer",
        "user": {
            "id": 1,
            "username": "wenmind_user",
            "nickname": "wenmind_user",
            "created_at": "2026-04-23T12:00:00"
        }
    }
}
```

**错误响应：**

| 错误码 | 条件 |
|--------|------|
| `41004` | 用户名或密码错误（统一提示，不暴露具体哪个错误） |

---

### GET `/api/v1/users/me`

获取当前登录用户信息。需要 JWT 认证。

**请求头：**

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "success",
    "data": {
        "id": 1,
        "username": "wenmind_user",
        "nickname": "wenmind_user",
        "created_at": "2026-04-23T12:00:00"
    }
}
```

---

## 📄 内容模块

### GET `/api/v1/content/joke`

获取随机笑话。需要 JWT 认证。

**请求头：**

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "success",
    "data": {
        "id": "a1b2c3d4e5f6g7h8",
        "content": "为什么程序员总分不清万圣节和圣诞节？因为 Oct 31 等于 Dec 25。",
        "author": "",
        "category": "joke",
        "source": "JokeAPI"
    }
}
```

---

### GET `/api/v1/content/quote`

获取随机鸡汤文案。需要 JWT 认证。

**请求头：**

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "success",
    "data": {
        "id": "b2c3d4e5f6g7h8i9",
        "content": "生活就像一盒巧克力，你永远不知道下一颗是什么味道。",
        "author": "阿甘正传",
        "category": "quote",
        "source": "local_fallback"
    }
}
```

---

### GET `/api/v1/content/history`

获取当前用户的浏览历史。需要 JWT 认证。

**请求头：**

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**查询参数：**

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `limit` | int | 50 | 返回条数，最大 200 |

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "success",
    "data": [
        {
            "id": 1,
            "content": "为什么程序员...",
            "author": "",
            "category": "joke",
            "source": "JokeAPI",
            "viewed_at": "2026-04-23T12:05:00"
        }
    ]
}
```

---

### DELETE `/api/v1/content/history`

清空当前用户的浏览历史。需要 JWT 认证。

**请求头：**

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "浏览历史已清空",
    "data": null
}
```

---

## ⭐ 收藏模块

### POST `/api/v1/favorites/`

添加收藏。需要 JWT 认证。

**请求头：**

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**请求体：**

```json
{
    "content": "生活就像一盒巧克力...",
    "author": "阿甘正传",
    "category": "quote",
    "source": "local_fallback"
}
```

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "收藏成功",
    "data": {
        "id": 1,
        "content": "生活就像一盒巧克力...",
        "author": "阿甘正传",
        "category": "quote",
        "source": "local_fallback",
        "created_at": "2026-04-23T12:10:00"
    }
}
```

**错误响应：**

| 错误码 | 条件 |
|--------|------|
| `42003` | 已收藏过相同内容 |

---

### GET `/api/v1/favorites/`

查看收藏列表。需要 JWT 认证。

**请求头：**

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**查询参数：**

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `category` | string | - | 筛选类型：`joke` 或 `quote`，不传返回全部 |
| `limit` | int | 50 | 返回条数，最大 200 |

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "success",
    "data": [
        {
            "id": 1,
            "content": "生活就像一盒巧克力...",
            "author": "阿甘正传",
            "category": "quote",
            "source": "local_fallback",
            "created_at": "2026-04-23T12:10:00"
        }
    ]
}
```

---

### DELETE `/api/v1/favorites/{favorite_id}`

删除指定收藏。需要 JWT 认证。

**请求头：**

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

**路径参数：**

| 参数 | 类型 | 说明 |
|------|------|------|
| `favorite_id` | int | 收藏记录的 ID |

**成功响应（200）：**

```json
{
    "code": 200,
    "message": "已取消收藏",
    "data": null
}
```

**错误响应：**

| 错误码 | 条件 |
|--------|------|
| `42004` | 收藏记录不存在 |
| `40300` | 无权删除此收藏（非本人的收藏） |

---

## 🔐 认证说明

1. **注册/登录**后获取 `access_token`（JWT，有效期 7 天）
2. 后续请求在 `Authorization` 头中携带 `Bearer <token>`
3. 前端 `api.js` 自动处理令牌的存储、注入和过期跳转

### 请求示例

```javascript
// 使用前端封装的 api.js
const data = await contentApi.getRandomJoke();
console.log(data.content);

// 或使用原生 fetch
fetch('http://localhost:8000/api/v1/content/joke', {
    headers: {
        'Authorization': 'Bearer eyJhbGciOiJIUzI1NiIs...'
    }
});
```

---

## ⚠️ 注意事项

1. 所有需要认证的接口，缺少或使用无效的 JWT 令牌会返回 `40100` 错误
2. 第三方 API 调用失败时自动使用本地备选数据，不影响核心功能
3. SQLite 数据库文件位于 `backend/wenmind.db`，可直接复制备份
4. 前端建议通过 VS Code Live Server 打开，避免 CORS 问题
