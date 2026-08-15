<script setup>
import { ref, computed, watch } from 'vue'
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
const excludePlayerIds = ref([])
const useIncludePlayer = ref(false)
const includePlayerIds = ref([])
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

watch(useExcludePlayer, v => { if (!v) excludePlayerIds.value = [] })
watch(useIncludePlayer, v => { if (!v) includePlayerIds.value = [] })

// 筛选后的对局
const filteredGames = computed(() => {
  return props.games.filter(g => {
    if (excludeVoid.value && g.status === GAME_STATUS.VOIDED) return false
    if (useTimeRange.value) {
      const gt = toDate(g.playedAt)
      if (timeFrom.value) {
        const f = new Date(timeFrom.value + 'T00:00:00')
        if (gt < f) return false
      }
      if (timeTo.value) {
        const t = new Date(timeTo.value + 'T23:59:59')
        if (gt > t) return false
      }
    }
    if (useExcludePlayer.value && excludePlayerIds.value.length) {
      const ids = new Set(excludePlayerIds.value.map(Number))
      if (g.players.some(p => ids.has(p.playerId))) return false
    }
    if (useIncludePlayer.value && includePlayerIds.value.length) {
      const ids = new Set(includePlayerIds.value.map(Number))
      for (const id of ids) {
        if (!g.players.some(p => p.playerId === id)) return false
      }
    }
    return true
  })
})

// 已勾选的对局
const selectedGames = computed(() =>
  filteredGames.value.filter(g => g.selected && g.status !== GAME_STATUS.VOIDED)
)

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

function toDate(s) { return new Date(String(s).replace(' ', 'T')) }

function formatTime(iso) {
  const d = toDate(iso)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}
function scoreClass(score) {
  return score > 0 ? 'score-positive' : score < 0 ? 'score-negative' : ''
}
function sign(score) { return score > 0 ? '+' : '' }
function rate(wins, games) {
  return games > 0 ? (wins / games * 100).toFixed(0) + '%' : '-'
}

async function toggleSelected(g, selected) {
  try {
    await api.updateGameSelected(g.id, selected)
    emit('refresh-games')
  } catch (e) {
    emit('toast', e.message, 'error')
  }
}

async function selectAll(select) {
  if (bulkSelecting.value) return

  const targets = filteredGames.value.filter(g => g.selected !== select)
  if (targets.length === 0) {
    emit('toast', select ? '已全部勾选' : '已全部取消勾选', 'info')
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
    emit('toast', e.message, 'error')
  } finally {
    bulkSelecting.value = false
  }
}
</script>

<template>
  <div>
    <!-- 筛选条件 -->
    <el-card class="block-card">
      <template #header>筛选条件</template>
      <el-checkbox v-model="excludeVoid">排除已作废记录</el-checkbox>

      <div class="filter-row">
        <el-checkbox v-model="useTimeRange">时间范围</el-checkbox>
        <div v-show="useTimeRange" class="filter-inline">
          <el-date-picker v-model="timeFrom" type="date" value-format="YYYY-MM-DD" placeholder="开始日期" />
          <span>至</span>
          <el-date-picker v-model="timeTo" type="date" value-format="YYYY-MM-DD" placeholder="结束日期" />
        </div>
      </div>

      <div class="filter-row">
        <el-checkbox v-model="useExcludePlayer">排除包含以下玩家的对局</el-checkbox>
        <el-select
          v-show="useExcludePlayer"
          v-model="excludePlayerIds"
          multiple
          collapse-tags
          collapse-tags-tooltip
          clearable
          placeholder="选择玩家"
          class="player-select"
        >
          <el-option v-for="p in allPlayers" :key="p.id" :label="p.name" :value="p.id" />
        </el-select>
      </div>

      <div class="filter-row">
        <el-checkbox v-model="useIncludePlayer">同时包含以下玩家的对局</el-checkbox>
        <el-select
          v-show="useIncludePlayer"
          v-model="includePlayerIds"
          multiple
          collapse-tags
          collapse-tags-tooltip
          clearable
          placeholder="选择玩家"
          class="player-select"
        >
          <el-option v-for="p in allPlayers" :key="p.id" :label="p.name" :value="p.id" />
        </el-select>
      </div>
    </el-card>

    <!-- 统计概览 -->
    <el-card class="block-card">
      <template #header>统计概览</template>
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

      <el-empty v-if="playerStats.length === 0" description="没有符合条件的统计数据" />
      <template v-else>
        <el-table :data="playerStats" style="width: 100%; margin-top: 12px" :row-class-name="() => 'stats-row'">
          <el-table-column type="index" label="排名" width="60" />
          <el-table-column prop="name" label="玩家" />
          <el-table-column prop="games" label="对局" width="70" />
          <el-table-column label="总积分" width="90">
            <template #default="{ row }">
              <span :class="scoreClass(row.total)" class="total">{{ sign(row.total) }}{{ row.total }}</span>
            </template>
          </el-table-column>
          <el-table-column label="场均" width="80">
            <template #default="{ row }">
              {{ row.games > 0 ? (row.total / row.games).toFixed(1) : 0 }}
            </template>
          </el-table-column>
          <el-table-column label="最高" width="80">
            <template #default="{ row }">
              <span class="score-positive">{{ row.max !== -Infinity ? sign(row.max) + row.max : '-' }}</span>
            </template>
          </el-table-column>
          <el-table-column label="最低" width="80">
            <template #default="{ row }">
              <span class="score-negative">{{ row.min !== Infinity ? sign(row.min) + row.min : '-' }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="wins" label="胜" width="60" />
          <el-table-column prop="losses" label="负" width="60" />
          <el-table-column label="胜率" width="70">
            <template #default="{ row }">{{ rate(row.wins, row.games) }}</template>
          </el-table-column>
        </el-table>

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
    </el-card>

    <!-- 记录列表 -->
    <el-card class="block-card">
      <template #header>
        <div class="list-header">
          <span>记录列表</span>
          <div class="list-actions">
            <el-button text size="small" :disabled="bulkSelecting" @click="selectAll(true)">全选</el-button>
            <el-button text size="small" :disabled="bulkSelecting" @click="selectAll(false)">取消全选</el-button>
          </div>
        </div>
      </template>

      <el-empty v-if="filteredGames.length === 0" description="没有符合条件的记录" />
      <div v-else class="record-list">
        <div
          v-for="g in [...filteredGames].sort((a, b) => toDate(b.playedAt) - toDate(a.playedAt))"
          :key="g.id"
          class="record-item"
          :class="{ voided: g.status === GAME_STATUS.VOIDED, unchecked: !g.selected }"
        >
          <el-checkbox :model-value="g.selected" @change="toggleSelected(g, $event)" />
          <div class="record-info">
            <div class="record-time">
              🕐 {{ formatTime(g.playedAt) }}
              <el-tag v-if="g.status === GAME_STATUS.VOIDED" type="info" size="small" effect="plain">已作废</el-tag>
            </div>
            <div class="record-players">
              <span v-for="p in g.players" :key="p.playerId">
                {{ p.playerName }}<span :class="scoreClass(p.score)">{{ sign(p.score) }}{{ p.score }}</span>
              </span>
            </div>
          </div>
        </div>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.block-card { margin-bottom: 16px; }
.filter-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  padding: 10px 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.filter-row:last-child { border-bottom: none; }
.filter-inline { display: flex; align-items: center; gap: 8px; }
.player-select { min-width: 280px; flex: 1; max-width: 520px; }

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 12px;
}
.summary-card { background: var(--el-fill-color-light); border-radius: 8px; padding: 12px; text-align: center; }
.summary-card .label { font-size: 12px; color: var(--el-text-color-secondary); }
.summary-card .value { font-size: 20px; font-weight: bold; margin-top: 4px; }

.score-positive { color: var(--el-color-success); }
.score-negative { color: var(--el-color-danger); }
.total { font-weight: bold; }

.bar-chart { margin-top: 16px; }
.bar-chart h3 { font-size: 14px; color: var(--el-color-primary); margin-bottom: 12px; }
.bar-item { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; }
.bar-label { width: 60px; font-size: 13px; text-align: right; }
.bar-container {
  flex: 1;
  height: 24px;
  background: var(--el-fill-color);
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
.bar-positive { background: var(--el-color-success-light-3); color: var(--el-color-success); }
.bar-negative { background: var(--el-color-danger-light-3); color: var(--el-color-danger); }

.list-header { display: flex; justify-content: space-between; align-items: center; }
.list-header .list-actions { display: flex; gap: 4px; }
.record-list { max-height: 600px; overflow-y: auto; }
.record-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px;
  background: var(--el-fill-color-light);
  border-radius: 8px;
  margin-bottom: 8px;
  border: 1px solid var(--el-border-color-lighter);
}
.record-item.voided { opacity: 0.5; }
.record-item.unchecked { opacity: 0.4; }
.record-info { flex: 1; font-size: 13px; }
.record-time { color: var(--el-text-color-secondary); font-size: 12px; }
.record-players { color: var(--el-text-color-primary); margin-top: 2px; }
.record-players > span { margin-right: 8px; }

@media (max-width: 600px) {
  :deep(.el-table) { font-size: 12px; }
}
</style>
