<script setup>
import { ref, computed, watch } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { api } from '../../utils/api.js'
import { toast } from '../../utils/ui.js'
import { THEMES, getTheme, chooseTheme } from '../../utils/theme.js'

const GAME_STATUS = { ACTIVE: 0, VOIDED: 1 }

const players = ref([])
const games = ref([])
const theme = ref(getTheme())

// 筛选条件
const excludeVoid = ref(true)
const useTimeRange = ref(false)
const timeFrom = ref('')
const timeTo = ref('')
const useExcludePlayer = ref(false)
const excludePlayerIds = ref([]) // number[]
const useIncludePlayer = ref(false)
const includePlayerIds = ref([]) // number[]
const bulkSelecting = ref(false)

const themeLabel = computed(
  () => THEMES.find((t) => t.value === theme.value)?.label || '麻将绿'
)

// 所有出现在对局中的玩家（含已从玩家池删除的）
const allPlayers = computed(() => {
  const map = new Map()
  players.value.forEach((p) => map.set(p.id, p.name))
  games.value.forEach((g) => {
    g.players.forEach((p) => {
      if (!map.has(p.playerId)) map.set(p.playerId, p.playerName)
    })
  })
  return Array.from(map.entries()).map(([id, name]) => ({ id, name }))
})

watch(useExcludePlayer, (v) => {
  if (!v) excludePlayerIds.value = []
})
watch(useIncludePlayer, (v) => {
  if (!v) includePlayerIds.value = []
})

// 筛选后的对局
const filteredGames = computed(() => {
  return games.value.filter((g) => {
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
    if (useExcludePlayer.value && excludePlayerIds.value.length) {
      const ids = new Set(excludePlayerIds.value.map(Number))
      if (g.players.some((p) => ids.has(p.playerId))) return false
    }
    if (useIncludePlayer.value && includePlayerIds.value.length) {
      const ids = new Set(includePlayerIds.value.map(Number))
      for (const id of ids) {
        if (!g.players.some((p) => p.playerId === id)) return false
      }
    }
    return true
  })
})

// 已勾选的对局
const selectedGames = computed(() =>
  filteredGames.value.filter((g) => g.selected && g.status !== GAME_STATUS.VOIDED)
)

// 玩家统计
const playerStats = computed(() => {
  const map = {}
  selectedGames.value.forEach((g) => {
    g.players.forEach((p) => {
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
  Math.max(...playerStats.value.map((p) => Math.abs(p.total)), 1)
)

function onTheme() {
  chooseTheme((t) => {
    theme.value = t
  })
}

function onExcludeVoid(e) {
  excludeVoid.value = e.detail.value.length > 0
}
function onUseTimeRange(e) {
  useTimeRange.value = e.detail.value.length > 0
}
function onUseExcludePlayer(e) {
  useExcludePlayer.value = e.detail.value.length > 0
}
function onUseIncludePlayer(e) {
  useIncludePlayer.value = e.detail.value.length > 0
}
function onExcludePlayerIds(e) {
  excludePlayerIds.value = e.detail.value.map(Number)
}
function onIncludePlayerIds(e) {
  includePlayerIds.value = e.detail.value.map(Number)
}

onShow(() => {
  theme.value = getTheme()
  loadAll()
})

async function loadAll() {
  try {
    const [ps, gs] = await Promise.all([api.getPlayers(), api.getGames()])
    players.value = ps
    games.value = gs
  } catch (e) {
    toast(e.message, 'error')
  }
}

function formatTime(iso) {
  const d = new Date(iso)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}
function pad(n) {
  return String(n).padStart(2, '0')
}

function scoreClass(score) {
  return score > 0 ? 'score-positive' : score < 0 ? 'score-negative' : ''
}
function sign(score) {
  return score > 0 ? '+' : ''
}
function rate(wins, games) {
  return games > 0 ? ((wins / games) * 100).toFixed(0) + '%' : '-'
}

async function toggleSelected(g, selected) {
  try {
    await api.updateGameSelected(g.id, selected)
    await loadAll()
  } catch (e) {
    toast(e.message, 'error')
  }
}

async function selectAll(select) {
  if (bulkSelecting.value) return

  const targets = filteredGames.value.filter((g) => g.selected !== select)
  if (targets.length === 0) {
    toast(select ? '已全部勾选' : '已全部取消勾选', 'info')
    return
  }

  bulkSelecting.value = true
  try {
    await Promise.all(targets.map((g) => api.updateGameSelected(g.id, select)))
    await loadAll()
  } catch (e) {
    await loadAll()
    toast(e.message, 'error')
  } finally {
    bulkSelecting.value = false
  }
}
</script>

<template>
  <view class="page-wrap" :class="'theme-' + theme">
    <view class="top-bar">
      <text class="top-title">🀄 麻将荣誉榜</text>
      <button class="btn btn-text" @tap="onTheme">主题：{{ themeLabel }}</button>
    </view>

    <!-- 筛选条件 -->
    <view class="card">
      <view class="card-title">筛选条件</view>

      <checkbox-group @change="onExcludeVoid">
        <label class="switch-row">
          <checkbox value="1" :checked="excludeVoid" color="#e8c547" style="transform: scale(0.8)" />
          <text>排除已作废记录</text>
        </label>
      </checkbox-group>

      <view class="filter-row">
        <checkbox-group @change="onUseTimeRange">
          <label class="switch-row">
            <checkbox value="1" :checked="useTimeRange" color="#e8c547" style="transform: scale(0.8)" />
            <text>时间范围</text>
          </label>
        </checkbox-group>
        <view v-if="useTimeRange" class="filter-inline">
          <picker mode="date" :value="timeFrom" @change="(e) => (timeFrom = e.detail.value)">
            <view class="picker-box">{{ timeFrom || '开始日期' }}</view>
          </picker>
          <text class="dim">至</text>
          <picker mode="date" :value="timeTo" @change="(e) => (timeTo = e.detail.value)">
            <view class="picker-box">{{ timeTo || '结束日期' }}</view>
          </picker>
        </view>
      </view>

      <view class="filter-row">
        <checkbox-group @change="onUseExcludePlayer">
          <label class="switch-row">
            <checkbox value="1" :checked="useExcludePlayer" color="#e8c547" style="transform: scale(0.8)" />
            <text>排除包含以下玩家的对局</text>
          </label>
        </checkbox-group>
        <checkbox-group v-if="useExcludePlayer" class="player-check-group" @change="onExcludePlayerIds">
          <label v-for="p in allPlayers" :key="p.id" class="player-check">
            <checkbox
              :value="String(p.id)"
              :checked="excludePlayerIds.includes(p.id)"
              color="#e8c547"
              style="transform: scale(0.7)"
            />
            <text class="check-name">{{ p.name }}</text>
          </label>
        </checkbox-group>
      </view>

      <view class="filter-row">
        <checkbox-group @change="onUseIncludePlayer">
          <label class="switch-row">
            <checkbox value="1" :checked="useIncludePlayer" color="#e8c547" style="transform: scale(0.8)" />
            <text>同时包含以下玩家的对局</text>
          </label>
        </checkbox-group>
        <checkbox-group v-if="useIncludePlayer" class="player-check-group" @change="onIncludePlayerIds">
          <label v-for="p in allPlayers" :key="p.id" class="player-check">
            <checkbox
              :value="String(p.id)"
              :checked="includePlayerIds.includes(p.id)"
              color="#e8c547"
              style="transform: scale(0.7)"
            />
            <text class="check-name">{{ p.name }}</text>
          </label>
        </checkbox-group>
      </view>
    </view>

    <!-- 统计概览 -->
    <view class="card">
      <view class="card-title">统计概览</view>
      <view class="summary-grid">
        <view class="summary-card">
          <view class="summary-label">纳入统计</view>
          <view class="summary-value">{{ selectedGames.length }}</view>
        </view>
        <view class="summary-card">
          <view class="summary-label">参与玩家</view>
          <view class="summary-value">{{ playerStats.length }}</view>
        </view>
        <view class="summary-card">
          <view class="summary-label">筛选显示</view>
          <view class="summary-value">{{ filteredGames.length }}</view>
        </view>
        <view class="summary-card">
          <view class="summary-label">总记录数</view>
          <view class="summary-value">{{ games.length }}</view>
        </view>
      </view>

      <view v-if="playerStats.length === 0" class="empty dim">没有符合条件的统计数据</view>

      <block v-else>
        <!-- 玩家统计表（横向滚动） -->
        <scroll-view scroll-x class="table-scroll">
          <view class="table">
            <view class="tr th">
              <text class="td w-rank">排名</text>
              <text class="td w-name">玩家</text>
              <text class="td w-num">对局</text>
              <text class="td w-total">总积分</text>
              <text class="td w-num">场均</text>
              <text class="td w-num">最高</text>
              <text class="td w-num">最低</text>
              <text class="td w-num">胜</text>
              <text class="td w-num">负</text>
              <text class="td w-num">胜率</text>
            </view>
            <view v-for="(row, idx) in playerStats" :key="row.id" class="tr">
              <text class="td w-rank">{{ idx + 1 }}</text>
              <text class="td w-name">{{ row.name }}</text>
              <text class="td w-num">{{ row.games }}</text>
              <text class="td w-total" :class="scoreClass(row.total)">{{ sign(row.total) }}{{ row.total }}</text>
              <text class="td w-num">{{ row.games > 0 ? (row.total / row.games).toFixed(1) : 0 }}</text>
              <text class="td w-num score-positive">{{ row.max !== -Infinity ? sign(row.max) + row.max : '-' }}</text>
              <text class="td w-num score-negative">{{ row.min !== Infinity ? sign(row.min) + row.min : '-' }}</text>
              <text class="td w-num">{{ row.wins }}</text>
              <text class="td w-num">{{ row.losses }}</text>
              <text class="td w-num">{{ rate(row.wins, row.games) }}</text>
            </view>
          </view>
        </scroll-view>

        <!-- 柱状图 -->
        <view class="bar-chart">
          <view class="bar-title">总积分对比</view>
          <view v-for="p in playerStats" :key="p.id" class="bar-item">
            <text class="bar-label">{{ p.name }}</text>
            <view class="bar-container">
              <view
                class="bar"
                :class="p.total >= 0 ? 'bar-positive' : 'bar-negative'"
                :style="{ width: Math.max((Math.abs(p.total) / maxAbs) * 100, 3) + '%' }"
              >{{ sign(p.total) }}{{ p.total }}</view>
            </view>
          </view>
        </view>
      </block>
    </view>

    <!-- 记录列表 -->
    <view class="card">
      <view class="list-header">
        <text class="card-title" style="margin-bottom: 0">记录列表</text>
        <view class="list-actions">
          <button class="btn btn-text" :disabled="bulkSelecting" @tap="selectAll(true)">全选</button>
          <button class="btn btn-text" :disabled="bulkSelecting" @tap="selectAll(false)">取消全选</button>
        </view>
      </view>

      <view v-if="filteredGames.length === 0" class="empty dim">没有符合条件的记录</view>
      <view v-else class="record-list">
        <view
          v-for="g in [...filteredGames].sort((a, b) => new Date(b.playedAt) - new Date(a.playedAt))"
          :key="g.id"
          class="record-item"
          :class="{ voided: g.status === GAME_STATUS.VOIDED, unchecked: !g.selected }"
        >
          <view class="check-box" :class="{ checked: g.selected }" @tap="toggleSelected(g, !g.selected)">
            <text v-if="g.selected">✓</text>
          </view>
          <view class="record-info">
            <view class="record-time">
              <text>🕐 {{ formatTime(g.playedAt) }}</text>
              <text v-if="g.status === GAME_STATUS.VOIDED" class="tag tag-info">已作废</text>
            </view>
            <view class="record-players">
              <text v-for="p in g.players" :key="p.playerId" class="rp">
                {{ p.playerName }}<text :class="scoreClass(p.score)">{{ sign(p.score) }}{{ p.score }}</text>
              </text>
            </view>
          </view>
        </view>
      </view>
    </view>
  </view>
</template>

<style scoped>
.top-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8rpx 4rpx 20rpx;
}
.top-title {
  font-size: 36rpx;
  font-weight: bold;
  color: var(--primary);
}

.switch-row {
  display: flex;
  align-items: center;
  gap: 8rpx;
  font-size: 28rpx;
}

.filter-row {
  padding: 20rpx 0;
  border-bottom: 1rpx solid var(--border);
}
.filter-row:last-child {
  border-bottom: none;
}
.filter-inline {
  display: flex;
  align-items: center;
  gap: 12rpx;
  margin-top: 16rpx;
}
.picker-box {
  flex: 1;
  height: 64rpx;
  line-height: 64rpx;
  background: var(--bg-fill);
  border-radius: 10rpx;
  padding: 0 16rpx;
  font-size: 26rpx;
  text-align: center;
}

.player-check-group {
  display: flex;
  flex-wrap: wrap;
  gap: 12rpx;
  margin-top: 16rpx;
}
.player-check {
  display: flex;
  align-items: center;
  gap: 4rpx;
  background: var(--bg-fill);
  border-radius: 10rpx;
  padding: 8rpx 16rpx;
}
.check-name {
  font-size: 24rpx;
}

.summary-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx;
}
.summary-card {
  flex: 1;
  min-width: 150rpx;
  background: var(--bg-fill);
  border-radius: 12rpx;
  padding: 20rpx;
  text-align: center;
}
.summary-label {
  font-size: 22rpx;
  color: var(--text-secondary);
}
.summary-value {
  font-size: 40rpx;
  font-weight: bold;
  margin-top: 8rpx;
}

.empty {
  text-align: center;
  padding: 60rpx 0;
}

/* 表格 */
.table-scroll {
  margin-top: 24rpx;
  white-space: nowrap;
}
.table {
  display: inline-block;
  min-width: 100%;
}
.tr {
  display: flex;
  align-items: center;
  border-bottom: 1rpx solid var(--border);
  padding: 16rpx 0;
}
.th {
  color: var(--text-secondary);
  font-size: 24rpx;
}
.td {
  font-size: 26rpx;
  text-align: center;
  flex-shrink: 0;
}
.w-rank { width: 80rpx; }
.w-name { width: 140rpx; text-align: left; padding-left: 8rpx; }
.w-num { width: 110rpx; }
.w-total { width: 140rpx; font-weight: bold; }

/* 柱状图 */
.bar-chart {
  margin-top: 32rpx;
}
.bar-title {
  font-size: 28rpx;
  color: var(--primary);
  margin-bottom: 20rpx;
}
.bar-item {
  display: flex;
  align-items: center;
  gap: 16rpx;
  margin-bottom: 16rpx;
}
.bar-label {
  width: 120rpx;
  font-size: 24rpx;
  text-align: right;
  flex-shrink: 0;
}
.bar-container {
  flex: 1;
  height: 48rpx;
  background: var(--bg-fill);
  border-radius: 8rpx;
  overflow: hidden;
}
.bar {
  height: 100%;
  border-radius: 8rpx;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding-right: 12rpx;
  font-size: 22rpx;
  font-weight: 600;
  min-width: 60rpx;
}
.bar-positive {
  background: rgba(76, 175, 80, 0.25);
  color: var(--success);
}
.bar-negative {
  background: rgba(239, 83, 80, 0.25);
  color: var(--danger);
}

/* 记录列表 */
.list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.list-actions {
  display: flex;
  gap: 8rpx;
}
.record-list {
  margin-top: 16rpx;
}
.record-item {
  display: flex;
  align-items: center;
  gap: 20rpx;
  padding: 20rpx;
  background: var(--bg-fill);
  border-radius: 12rpx;
  margin-bottom: 16rpx;
  border: 1rpx solid var(--border);
}
.record-item.voided {
  opacity: 0.5;
}
.record-item.unchecked {
  opacity: 0.4;
}
.check-box {
  width: 44rpx;
  height: 44rpx;
  border-radius: 8rpx;
  border: 2rpx solid var(--border);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--primary-text);
  font-size: 28rpx;
  flex-shrink: 0;
}
.check-box.checked {
  background: var(--primary);
  border-color: var(--primary);
}
.record-info {
  flex: 1;
  font-size: 26rpx;
}
.record-time {
  color: var(--text-secondary);
  font-size: 22rpx;
  display: flex;
  align-items: center;
  gap: 12rpx;
}
.record-players {
  margin-top: 6rpx;
  color: var(--text-primary);
}
.rp {
  margin-right: 16rpx;
}
</style>
