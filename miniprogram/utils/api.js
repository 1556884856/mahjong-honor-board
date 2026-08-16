// API 请求封装（与网页端 api.js 接口保持一致）
// ============================================================
// 服务器地址支持在「设置页」中随时修改，修改后保存在本机缓存，
// 下次启动仍生效。未设置时使用下面的默认地址。
//   - 默认（体验版调试）：http://47.85.163.218/api
//     后端 dotnet 监听 127.0.0.1:5080（不对公网暴露），
//     对外统一走 Nginx 80 端口 /api 反向代理。
//     手机开启「右上角 ... → 开发调试」后可不校验域名/HTTPS
//   - 正式上线：需换成 https://备案域名/api
// ============================================================
const DEFAULT_BASE_URL = 'http://47.85.163.218/api'
const STORAGE_KEY = 'mahjong_api_base_url'
const TOKEN_KEY = 'mahjong_auth_token'
const OPENID_KEY = 'mahjong_openid'
const TOKEN_EXPIRES_AT_KEY = 'mahjong_auth_expires_at'

function getBaseUrl() {
  const saved = uni.getStorageSync(STORAGE_KEY)
  return saved || DEFAULT_BASE_URL
}

function setBaseUrl(url) {
  const normalized = url.trim().replace(/\/+$/, '')
  uni.setStorageSync(STORAGE_KEY, normalized)
  clearAuth()
  return normalized
}

function resetBaseUrl() {
  uni.removeStorageSync(STORAGE_KEY)
  clearAuth()
}

// ===== 登录态 =====
function getToken() {
  return uni.getStorageSync(TOKEN_KEY) || ''
}

function getOpenid() {
  return uni.getStorageSync(OPENID_KEY) || ''
}

function getTokenExpiresAt() {
  const value = uni.getStorageSync(TOKEN_EXPIRES_AT_KEY)
  const n = Number(value)
  return Number.isFinite(n) ? n : 0
}

function setAuth(token, openid, expiresAt) {
  uni.setStorageSync(TOKEN_KEY, token)
  uni.setStorageSync(OPENID_KEY, openid || '')
  const expires = new Date(expiresAt).getTime()
  uni.setStorageSync(TOKEN_EXPIRES_AT_KEY, Number.isFinite(expires) ? expires : 0)
}

function clearAuth() {
  uni.removeStorageSync(TOKEN_KEY)
  uni.removeStorageSync(OPENID_KEY)
  uni.removeStorageSync(TOKEN_EXPIRES_AT_KEY)
}

// 微信登录：wx.login 拿 code → 后端换 openid 并签发 token
function login() {
  return new Promise((resolve, reject) => {
    uni.login({
      provider: 'weixin',
      success: (loginRes) => {
        if (!loginRes.code) {
          reject(new Error('wx.login 未返回 code'))
          return
        }
        uni.request({
          url: getBaseUrl() + '/auth/login',
          method: 'POST',
          data: { code: loginRes.code },
          header: { 'Content-Type': 'application/json' },
          success: (res) => {
            if (res.statusCode === 200 && res.data && res.data.token) {
              setAuth(res.data.token, res.data.openid, res.data.expiresAt)
              resolve(res.data)
            } else if (res.statusCode === 403 && res.data && res.data.openid) {
              // 未在白名单：弹窗展示 openid，方便复制给管理员添加
              uni.showModal({
                title: '未授权',
                content: '你的微信 openid：\n' + res.data.openid + '\n\n请把它发给管理员添加到白名单。',
                showCancel: false,
              })
              reject(new Error(res.data.message || '未授权'))
            } else {
              const msg = (res.data && res.data.message) || `登录失败 (${res.statusCode})`
              reject(new Error(msg))
            }
          },
          fail: (err) => reject(new Error(err.errMsg || '网络请求失败')),
        })
      },
      fail: (err) => reject(new Error(err.errMsg || 'wx.login 失败')),
    })
  })
}

// 确保已登录（有 token 直接通过，无则登录）
async function ensureLogin() {
  if (getToken() && (!getTokenExpiresAt() || Date.now() < getTokenExpiresAt())) return true
  clearAuth()
  await login()
  return true
}

// 通用请求：写操作（POST/PATCH/DELETE）自动确保登录并带 token；401 时重登重试一次
function request(method, url, body) {
  const doRequest = () =>
    new Promise((resolve, reject) => {
      const header = { 'Content-Type': 'application/json' }
      const token = getToken()
      if (token) header['Authorization'] = 'Bearer ' + token

      uni.request({
        url: getBaseUrl() + url,
        method,
        data: body,
        header,
        success: (res) => {
          if (res.statusCode === 204) {
            resolve(null)
            return
          }
          if (res.statusCode === 401) {
            const err = new Error('登录已过期，请重试')
            err.status = 401
            reject(err)
            return
          }
          const data = res.data
          if (res.statusCode >= 200 && res.statusCode < 300) {
            resolve(data)
          } else {
            const msg = (data && data.message) || `请求失败 (${res.statusCode})`
            reject(new Error(msg))
          }
        },
        fail: (err) => {
          reject(new Error(err.errMsg || '网络请求失败'))
        },
      })
    })

  // 读接口公开，直接请求
  if (method === 'GET') return doRequest()

  // 写接口：先确保登录，再请求；若 401 则清 token 重登重试一次
  return ensureLogin()
    .then(doRequest)
    .catch((e) => {
      if (e && e.status === 401) {
        clearAuth()
        return ensureLogin().then(doRequest)
      }
      throw e
    })
}

export { getBaseUrl, setBaseUrl, resetBaseUrl, login, ensureLogin, getToken, getOpenid }

export const api = {
  // 玩家
  getPlayers: () => request('GET', '/players'),
  createPlayer: (name) => request('POST', '/players', { name }),
  updatePlayer: (id, name) => request('PATCH', `/players/${id}`, { name }),
  getPlayerNameHistory: (id) => request('GET', `/players/${id}/name-history`),
  deletePlayer: (id) => request('DELETE', `/players/${id}`),

  // 对局
  getGames: () => request('GET', '/games'),
  getGame: (id) => request('GET', `/games/${id}`),
  createGame: (payload) => request('POST', '/games', payload),
  updateGame: (id, payload) => request('PUT', `/games/${id}`, payload),
  updateGameStatus: (id, status) => request('PATCH', `/games/${id}/status`, { status }),
  updateGameSelected: (id, selected) => request('PATCH', `/games/${id}/selected`, { selected }),
  deleteGame: (id) => request('DELETE', `/games/${id}`),
}

export default api
