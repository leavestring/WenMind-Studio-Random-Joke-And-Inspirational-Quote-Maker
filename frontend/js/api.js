/**
 * WenMind Studio - API 请求封装层
 * 
 * 统一管理所有后端 API 的调用，自动处理：
 * - JWT 令牌注入
 * - 统一错误处理
 * - 请求/响应格式转换
 */

// ============================================================
// 配置
// ============================================================

// API 基础地址 - 根据实际部署情况修改
const API_BASE_URL = 'http://localhost:8000';

// 本地存储 Key
const TOKEN_KEY = 'wenmind_token';
const USER_KEY = 'wenmind_user';

// ============================================================
// 令牌管理
// ============================================================

function getToken() {
    return localStorage.getItem(TOKEN_KEY);
}

function setToken(token) {
    localStorage.setItem(TOKEN_KEY, token);
}

function removeToken() {
    localStorage.removeItem(TOKEN_KEY);
}

function getStoredUser() {
    try {
        const user = localStorage.getItem(USER_KEY);
        return user ? JSON.parse(user) : null;
    } catch {
        return null;
    }
}

function setStoredUser(user) {
    localStorage.setItem(USER_KEY, JSON.stringify(user));
}

function removeStoredUser() {
    localStorage.removeItem(USER_KEY);
}

function isLoggedIn() {
    return !!getToken();
}

function logout() {
    removeToken();
    removeStoredUser();
    window.location.href = 'login.html';
}

// ============================================================
// 核心请求函数
// ============================================================

/**
 * 发送 API 请求的通用函数
 * 
 * @param {string} endpoint - API 路径（如 /api/v1/users/login）
 * @param {object} options - 请求选项
 * @param {string} options.method - HTTP 方法（GET/POST/PUT/DELETE）
 * @param {object} options.body - 请求体（会 JSON 序列化）
 * @param {boolean} options.requireAuth - 是否需要认证
 * @returns {Promise<object>} 统一响应 { code, message, data }
 */
async function apiRequest(endpoint, options = {}) {
    const {
        method = 'GET',
        body = null,
        requireAuth = true,
    } = options;

    const headers = {
        'Content-Type': 'application/json',
    };

    // 需要认证时注入 JWT 令牌
    if (requireAuth) {
        const token = getToken();
        if (!token) {
            // 未登录，重定向到登录页
            window.location.href = 'login.html';
            throw new Error('未登录');
        }
        headers['Authorization'] = `Bearer ${token}`;
    }

    try {
        const response = await fetch(`${API_BASE_URL}${endpoint}`, {
            method,
            headers,
            body: body ? JSON.stringify(body) : null,
        });

        const result = await response.json();

        // 处理业务错误
        if (result.code !== 200) {
            // 401 未授权 → 跳转登录
            if (result.code === 40100 || result.code === 40101 || result.code === 40102) {
                logout();
                throw new Error(result.message || '登录已过期');
            }
            throw new Error(result.message || '请求失败');
        }

        return result;
    } catch (error) {
        // 网络错误
        if (error.name === 'TypeError' && error.message.includes('fetch')) {
            throw new Error('网络连接失败，请检查服务器是否启动');
        }
        throw error;
    }
}

// ============================================================
// 用户相关 API
// ============================================================

const userApi = {
    /**
     * 用户注册
     * @param {string} username - 用户名
     * @param {string} password - 密码
     * @param {string} confirmPassword - 确认密码
     * @returns {Promise<object>} { access_token, token_type, user }
     */
    async register(username, password, confirmPassword) {
        const result = await apiRequest('/api/v1/users/register', {
            method: 'POST',
            body: { username, password, confirm_password: confirmPassword },
            requireAuth: false,
        });
        // 保存令牌和用户信息
        setToken(result.data.access_token);
        setStoredUser(result.data.user);
        return result.data;
    },

    /**
     * 用户登录
     * @param {string} username - 用户名
     * @param {string} password - 密码
     * @returns {Promise<object>} { access_token, token_type, user }
     */
    async login(username, password) {
        const result = await apiRequest('/api/v1/users/login', {
            method: 'POST',
            body: { username, password },
            requireAuth: false,
        });
        // 保存令牌和用户信息
        setToken(result.data.access_token);
        setStoredUser(result.data.user);
        return result.data;
    },

    /**
     * 获取当前用户信息
     * @returns {Promise<object>} { id, username, nickname, created_at }
     */
    async getProfile() {
        const result = await apiRequest('/api/v1/users/me');
        return result.data;
    },
};

// ============================================================
// 内容相关 API
// ============================================================

const contentApi = {
    /**
     * 获取随机笑话
     * @returns {Promise<object>} { id, content, author, category, source }
     */
    async getRandomJoke() {
        const result = await apiRequest('/api/v1/content/joke');
        return result.data;
    },

    /**
     * 获取随机鸡汤文案
     * @returns {Promise<object>} { id, content, author, category, source }
     */
    async getRandomQuote() {
        const result = await apiRequest('/api/v1/content/quote');
        return result.data;
    },

    /**
     * 获取浏览历史
     * @param {number} limit - 返回条数
     * @returns {Promise<Array>} 历史记录列表
     */
    async getHistory(limit = 50) {
        const result = await apiRequest(`/api/v1/content/history?limit=${limit}`);
        return result.data;
    },

    /**
     * 清空浏览历史
     * @returns {Promise<void>}
     */
    async clearHistory() {
        await apiRequest('/api/v1/content/history', { method: 'DELETE' });
    },
};

// ============================================================
// 收藏相关 API
// ============================================================

const favoriteApi = {
    /**
     * 添加收藏
     * @param {object} params
     * @param {string} params.content - 文案内容
     * @param {string} params.author - 作者
     * @param {string} params.category - 类型（joke/quote）
     * @param {string} params.source - 来源
     * @returns {Promise<object>} 收藏记录
     */
    async add(params) {
        const result = await apiRequest('/api/v1/favorites/', {
            method: 'POST',
            body: params,
        });
        return result.data;
    },

    /**
     * 获取收藏列表
     * @param {object} options
     * @param {string} options.category - 筛选类型（可选）
     * @param {number} options.limit - 返回条数
     * @returns {Promise<Array>} 收藏列表
     */
    async list(options = {}) {
        const { category = '', limit = 50 } = options;
        let url = `/api/v1/favorites/?limit=${limit}`;
        if (category) url += `&category=${category}`;
        const result = await apiRequest(url);
        return result.data;
    },

    /**
     * 删除收藏
     * @param {number} id - 收藏记录 ID
     * @returns {Promise<void>}
     */
    async remove(id) {
        await apiRequest(`/api/v1/favorites/${id}`, { method: 'DELETE' });
    },
};

// ============================================================
// Toast 提示工具
// ============================================================

function showToast(message, type = 'info', duration = 3000) {
    // 移除已有的 toast
    const existing = document.querySelector('.toast');
    if (existing) existing.remove();

    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.textContent = message;
    document.body.appendChild(toast);

    // 触发入场动画
    requestAnimationFrame(() => {
        toast.classList.add('show');
    });

    // 自动消失
    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 400);
    }, duration);
}
