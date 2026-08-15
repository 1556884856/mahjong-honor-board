<script setup>
import { ref, computed } from 'vue'
import { api } from '../api.js'

const props = defineProps({
  players: { type: Array, default: () => [] },
  games: { type: Array, default: () => [] },
})
const emit = defineEmits(['refresh-games', 'toast'])
const GAME_STATUS = { ACTIVE: 0, VOIDED: 1 }

// 筛选条件
const excludeVoid = ref(true)
const useTimeRange = ref(false)
const timeFrom = ref('')
const timeTo = ref('')
const useExcludePlayer = ref(false)
const excludePlayerId = ref('')
const useIncludePlayer = ref(false)
const includePlayerId = ref('')
const bulkSelecting = ref(false)

// 所有出现在对局中的玩家（含已从玩家池删除的）
const allPlayers = computed(() => {
  const map = new Map()
  props.players.forEach(p => map.set(p.id, p.name))
  props.games.forEach(g => {
    g.players.forEach(p => {
      if (!map.has(p.playerId)) map.set(p.playerId, p.playerName)
    })
  })
  return Array.from(map.entries()).map(([id, name]) => ({ id, name }))
})

// 筛选后的对局
const filteredGames = computed(() => {
  return props.games.filter(g => {
    if (excludeVoid.value && g.status === GAME_STATUS.VOIDED) return false
    if (useTimeRange.value) {
      const gt = new Date(g.playedAt)
      if (timeFrom.value) {
        const f = new Date(timeFrom.value)
        f.setHours(0, 0, 0, 0)
        if (gt < f) return false
      }
      if (timeTo.value) {
        const t = new Date(timeTo.value)
        t.setHours(23, 59, 59, 999)
        if (gt > t) return false
      }
    }
    if (useExcludePlayer.value && excludePlayerId.value) {
      if (g.players.some(p => p.playerId === Number(excludePlayerId.value))) return false
    }
    if (useIncludePlayer.value && includePlayerId.value) {
      if (!g.players.some(p => p.playerId === Number(includePlayerId.value))) return false
    }
    return true
  })
})

// 已勾选的对局
const selectedGames = computed(() => filteredGames.value.filter(g => g.selected))

// 玩家统计
const playerStats = computed(() => {
  const map = {}
  selectedGames.value.forEach(g => {
    g.players.forEach(p => {
      if (!map[p.playerId]) {
        map[p.playerId] = {
          id: p.playerId,
          name: p.playerName,
          games: 0,
          total: 0,
          wins: 0,
          losses: 0,
          max: -Infinity,
          min: Infinity,
        }
      }
      const ps = map[p.playerId]
      ps.games++
      ps.total += p.score
      if (p.score > 0) ps.wins++
      if (p.score < 0) ps.losses++
      if (p.score > ps.max) ps.max = p.score
      if (p.score < ps.min) ps.min = p.score
    })
  })
  return Object.values(map).sort((a, b) => b.total - a.total)
})

const maxAbs = computed(() =>
  Math.max(...playerStats.value.map(p => Math.abs(p.total)), 1)
)

function formatTime(iso) {
  const d = new Date(iso)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}
function scoreClass(score) {
  return score > 0 ? 'score-positive' : score < 0 ? 'score-negative' : ''
}
function sign(score) { return score > 0 ? '+' : '' }

async function toggleSelected(g, event) {
  const selected = event.target.checked
  try {
    await api.updateGameSelected(g.id, selected)
    emit('refresh-games')
  } catch (e) {
    emit('toast', e.message)
  }
}

async function selectAll(select) {
  if (bulkSelecting.value) return

  const targets = filteredGames.value.filter(g => g.selected !== select)
  if (targets.length === 0) {
    emit('toast', select ? '已全部勾选' : '已全部取消勾选')
    return
  }

  bulkSelecting.value = true
  try {
    await Promise.all(
      targets.map(g => api.updateGameSelected(g.id, select))
    )
    emit('refresh-games')
  } catch (e) {
    emit('refresh-games')
    emit('toast', e.message)
  } finally {
    bulkSelecting.value = false
  }
}
</script>

<template>
  <div>
    <!-- 筛选条件 -->
    <div class="card">
      <h2>筛选条件</h2>
      <div class="filter-group">
        <label class="filter-check">
          <input v-model="excludeVoid" type="checkbox">
          排除已作废记录
        </label>
      </div>

      <div class="filter-group">
        <label class="filter-check">
          <input v-model="useTimeRange" type="checkbox">
          时间范围
        </label>
        <div class="filter-content" :class="{ disabled: !useTimeRange }">
          <input v-model="timeFrom" type="date">
          <span>至</span>
          <input v-model="timeTo" type="date">
        </div>
      </div>

      <div class="filter-group">
        <label class="filter-check">
          <input v-model="useExcludePlayer" type="checkbox">
          排除包含某玩家的对局
        </label>
        <div class="filter-content" :class="{ disabled: !useExcludePlayer }">
          <select v-model="excludePlayerId">
            <option value="">选择玩家</option>
            <option v-for="p in allPlayers" :key="p.id" :value="p.id">{{ p.name }}</option>
          </select>
        </div>
      </div>

      <div class="filter-group">
        <label class="filter-check">
          <input v-model="useIncludePlayer" type="checkbox">
          仅包含某玩家的对局
        </label>
        <div class="filter-content" :class="{ disabled: !useIncludePlayer }">
          <select v-model="includePlayerId">
            <option value="">选择玩家</option>
            <option v-for="p in allPlayers" :key="p.id" :value="p.id">{{ p.name }}</option>
          </select>
        </div>
      </div>
    </div>

    <!-- 统计概览 -->
    <div class="card">
      <h2>统计概览</h2>
      <div class="summary-grid">
        <div class="summary-card">
          <div class="label">纳入统计</div>
          <div class="value">{{ selectedGames.length }}</div>
        </div>
        <div class="summary-card">
          <div class="label">参与玩家</div>
          <div class="value">{{ playerStats.length }}</div>
        </div>
        <div class="summary-card">
          <div class="label">筛选显示</div>
          <div class="value">{{ filteredGames.length }}</div>
        </div>
        <div class="summary-card">
          <div class="label">总记录数</div>
          <div class="value">{{ games.length }}</div>
        </div>
      </div>

      <p v-if="playerStats.length === 0" class="empty-hint">没有符合条件的统计数据</p>
      <template v-else>
        <table class="stats-table">
          <thead>
            <tr>
              <th>排名</th><th>玩家</th><th>对局</th><th>总积分</th><th>场均</th>
              <th>最高</th><th>最低</th><th>胜</th><th>负</th><th>胜率</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(p, i) in playerStats" :key="p.id">
              <td :class="{ 'rank-1': i === 0 }">{{ i + 1 }}</td>
              <td>{{ p.name }}</td>
              <td>{{ p.games }}</td>
              <td :class="scoreClass(p.total)" class="total">{{ sign(p.total) }}{{ p.total }}</td>
              <td>{{ p.games > 0 ? (p.total / p.games).toFixed(1) : 0 }}</td>
              <td class="score-positive">{{ p.max !== -Infinity ? sign(p.max) + p.max : '-' }}</td>
              <td class="score-negative">{{ p.min !== Infinity ? sign(p.min) + p.min : '-' }}</td>
              <td class="score-positive">{{ p.wins }}</td>
              <td class="score-negative">{{ p.losses }}</td>
              <td>{{ p.games > 0 ? (p.wins / p.games * 100).toFixed(0) + '%' : '-' }}</td>
            </tr>
          </tbody>
        </table>

        <div class="bar-chart">
          <h3>总积分对比</h3>
          <div v-for="p in playerStats" :key="p.id" class="bar-item">
            <div class="bar-label">{{ p.name }}</div>
            <div class="bar-container">
              <div
                class="bar"
                :class="p.total >= 0 ? 'bar-positive' : 'bar-negative'"
                :style="{ width: Math.max(Math.abs(p.total) / maxAbs * 100, 3) + '%' }"
              >{{ sign(p.total) }}{{ p.total }}</div>
            </div>
          </div>
        </div>
      </template>
    </div>

    <!-- 记录列表 -->
    <div class="card">
      <div class="action-bar">
        <h2 class="inline-h2">记录列表</h2>
        <div class="actions">
          <button class="btn btn-ghost btn-sm" :disabled="bulkSelecting" @click="selectAll(true)">全选</button>
          <button class="btn btn-ghost btn-sm" :disabled="bulkSelecting" @click="selectAll(false)">取消全选</button>
        </div>
      </div>

      <p v-if="filteredGames.length === 0" class="empty-hint">没有符合条件的记录</p>
      <div v-else class="record-list">
        <div
          v-for="g in [...filteredGames].sort((a, b) => new Date(b.playedAt) - new Date(a.playedAt))"
          :key="g.id"
          class="record-item"
          :class="{ voided: g.status === GAME_STATUS.VOIDED, unchecked: !g.selected }"
        >
          <input :checked="g.selected" type="checkbox" @change="toggleSelected(g, $event)">
          <div class="record-info">
            <div class="record-time">
              🕐 {{ formatTime(g.playedAt) }}
              <span v-if="g.status === GAME_STATUS.VOIDED" class="badge badge-voided">已作废</span>
            </div>
            <div class="record-players">
              <span v-for="p in g.players" :key="p.playerId">
                {{ p.playerName }}<span :class="scoreClass(p.score)">{{ sign(p.score) }}{{ p.score }}</span>
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.filter-group {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 0;
  border-bottom: 1px solid var(--border);
  flex-wrap: wrap;
}
.filter-group:last-child { border-bottom: none; }
.filter-check {
  font-size: 14px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}
.filter-check input[type="checkbox"] {
  width: 18px;
  height: 18px;
  accent-color: var(--accent);
  cursor: pointer;
}
.filter-content { display: flex; align-items: center; gap: 8px; }
.filter-content.disabled { opacity: 0.35; pointer-events: none; }

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 12px;
  margin-bottom: 16px;
}
.summary-card { background: var(--bg-hover); border-radius: 8px; padding: 12px; text-align: center; }
.summary-card .label { font-size: 12px; color: var(--text-dim); }
.summary-card .value { font-size: 20px; font-weight: bold; margin-top: 4px; }

.empty-hint { color: var(--text-dim); text-align: center; padding: 20px; }

.stats-table { width: 100%; border-collapse: collapse; margin-top: 12px; font-size: 14px; }
.stats-table th {
  background: var(--bg-hover);
  padding: 10px 6px;
  text-align: center;
  font-weight: 600;
  color: var(--text-dim);
  border-bottom: 2px solid var(--border);
  white-space: nowrap;
}
.stats-table td { padding: 10px 6px; text-align: center; border-bottom: 1px solid var(--border); }
.stats-table .total { font-weight: bold; }
.rank-1 { color: var(--accent); font-weight: bold; }

.bar-chart { margin-top: 16px; }
.bar-chart h3 { font-size: 14px; color: var(--accent); margin-bottom: 12px; }
.bar-item { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; }
.bar-label { width: 60px; font-size: 13px; text-align: right; }
.bar-container {
  flex: 1;
  height: 24px;
  background: var(--bg-input);
  border-radius: 4px;
  overflow: hidden;
}
.bar {
  height: 100%;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding-right: 8px;
  font-size: 12px;
  font-weight: 600;
  transition: width 0.3s;
  min-width: 30px;
}
.bar-positive { background: var(--green-dim); color: var(--green); }
.bar-negative { background: var(--red-dim); color: var(--red); }

.action-bar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.inline-h2 { font-size: 16px; color: var(--accent); }
.action-bar .actions { display: flex; gap: 8px; }

.record-list { max-height: 600px; overflow-y: auto; }
.record-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px;
  background: var(--bg-card);
  border-radius: 8px;
  margin-bottom: 8px;
  border: 1px solid var(--border);
  transition: opacity 0.2s;
}
.record-item.voided { opacity: 0.5; }
.record-item.unchecked { opacity: 0.4; }
.record-item input[type="checkbox"] {
  width: 18px;
  height: 18px;
  accent-color: var(--accent);
  cursor: pointer;
  flex-shrink: 0;
}
.record-info { flex: 1; font-size: 13px; }
.record-time { color: var(--text-dim); font-size: 12px; }
.record-players { color: var(--text); margin-top: 2px; }
.record-players > span { margin-right: 8px; }

@media (max-width: 600px) {
  .stats-table { font-size: 12px; }
  .stats-table th, .stats-table td { padding: 6px 3px; }
  .filter-group { flex-direction: column; align-items: flex-start; }
}
</style>
