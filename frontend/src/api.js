// API 请求封装
const BASE = import.meta.env.VITE_API_BASE || '/api'

// 管理员令牌通过构建环境变量注入，避免写死在源码中。
const ADMIN_TOKEN = import.meta.env.VITE_ADMIN_TOKEN || ''

async function request(method, url, body) {
  const options = { method, headers: {} }
  // 写操作带上管理员令牌，避免后端鉴权拦截
  if (method !== 'GET' && ADMIN_TOKEN) {
    options.headers['Authorization'] = 'Bearer ' + ADMIN_TOKEN
  }
  if (body !== undefined) {
    options.headers['Content-Type'] = 'application/json'
    options.body = JSON.stringify(body)
  }
  const res = await fetch(BASE + url, options)
  if (res.status === 204) return null
  const data = await res.json().catch(() => null)
  if (!res.ok) {
    throw new Error(data?.message || `请求失败 (${res.status})`)
  }
  return data
}

export const api = {
  // 玩家
  getPlayers: () => request('GET', '/players'),
  createPlayer: (name) => request('POST', '/players', { name }),
  deletePlayer: (id) => request('DELETE', `/players/${id}`),

  // 对局
  getGames: () => request('GET', '/games'),
  getGame: (id) => request('GET', `/games/${id}`),
  createGame: (payload) => request('POST', '/games', payload),
  updateGameStatus: (id, status) => request('PATCH', `/games/${id}/status`, { status }),
  updateGameSelected: (id, selected) => request('PATCH', `/games/${id}/selected`, { selected }),
  deleteGame: (id) => request('DELETE', `/games/${id}`),
}
