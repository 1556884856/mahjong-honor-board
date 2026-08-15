// API 请求封装
const BASE = import.meta.env.VITE_API_BASE || '/api'

async function request(method, url, body) {
  const options = { method, headers: {} }
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
