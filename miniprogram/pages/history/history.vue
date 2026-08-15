<script setup>
import { ref, computed, watch } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { api } from '../../utils/api.js'
import { toast, confirm } from '../../utils/ui.js'
import { THEMES, getTheme, chooseTheme } from '../../utils/theme.js'

const GAME_STATUS = { ACTIVE: 0, VOIDED: 1 }

const games = ref([])
const statusFilter = ref('all') // all | active | voided
const currentPage = ref(1)
const pageSize = ref(10)
const theme = ref(getTheme())

const themeLabel = computed(
  () => THEMES.find((t) => t.value === theme.value)?.label || '麻将绿'
)

const sortedGames = computed(() =>
  [...games.value].sort((a, b) => new Date(b.playedAt) - new Date(a.playedAt))
)

const activeCount = computed(
  () => sortedGames.value.filter((g) => g.status === GAME_STATUS.ACTIVE).length
)
const voidedCount = computed(
  () => sortedGames.value.filter((g) => g.status === GAME_STATUS.VOIDED).length
)

const filteredGames = computed(() => {
  if (statusFilter.value === 'active') {
    return sortedGames.value.filter((g) => g.status === GAME_STATUS.ACTIVE)
  }
  if (statusFilter.value === 'voided') {
    return sortedGames.value.filter((g) => g.status === GAME_STATUS.VOIDED)
  }
  return sortedGames.value
})

const totalPages = computed(() =>
  Math.max(1, Math.ceil(filteredGames.value.length / pageSize.value))
)

const pagedGames = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredGames.value.slice(start, start + pageSize.value)
})

watch(statusFilter, () => {
  currentPage.value = 1
})
watch(
  () => filteredGames.value.length,
  () => {
    if (currentPage.value > totalPages.value) currentPage.value = totalPages.value
  }
)

function onTheme() {
  chooseTheme((t) => {
    theme.value = t
  })
}

function onStatusFilter(e) {
  statusFilter.value = e.detail.value
}

onShow(() => {
  theme.value = getTheme()
  loadGames()
})

async function loadGames() {
  try {
    games.value = await api.getGames()
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
  return score > 0 ? 'score-positive' : score < 0 ? 'score-negative' : 'score-zero'
}
function sign(score) {
  return score > 0 ? '+' : ''
}

async function voidGame(game) {
  try {
    await api.updateGameStatus(game.id, GAME_STATUS.VOIDED)
    await loadGames()
    toast('已作废', 'success')
  } catch (e) {
    toast(e.message, 'error')
  }
}

async function restoreGame(game) {
  try {
    await api.updateGameStatus(game.id, GAME_STATUS.ACTIVE)
    await loadGames()
    toast('已恢复', 'success')
  } catch (e) {
    toast(e.message, 'error')
  }
}

function permanentDelete(game) {
  confirm('永久删除后无法恢复，确定删除？', async () => {
    try {
      await api.deleteGame(game.id)
      await loadGames()
      toast('已永久删除', 'success')
    } catch (e) {
      toast(e.message, 'error')
    }
  })
}
</script>

<template>
  <view class="page-wrap" :class="'theme-' + theme">
    <view class="top-bar">
      <text class="top-title">🀄 麻将荣誉榜</text>
      <button class="btn btn-text" @tap="onTheme">主题：{{ themeLabel }}</button>
    </view>

    <view class="count-bar">
      <text class="dim">
        共 <text class="count-strong">{{ sortedGames.length }}</text> 条记录
        （{{ activeCount }} 条正常，{{ voidedCount }} 条已作废）
      </text>
    </view>

    <view v-if="sortedGames.length === 0" class="empty dim">还没有对局记录，去「记录对局」开始第一局吧</view>

    <block v-else>
      <radio-group class="status-filter" @change="onStatusFilter">
        <label class="filter-item">
          <radio value="all" :checked="statusFilter === 'all'" color="#e8c547" style="transform: scale(0.7)" />
          <text>全部</text>
        </label>
        <label class="filter-item">
          <radio value="active" :checked="statusFilter === 'active'" color="#e8c547" style="transform: scale(0.7)" />
          <text>正常</text>
        </label>
        <label class="filter-item">
          <radio value="voided" :checked="statusFilter === 'voided'" color="#e8c547" style="transform: scale(0.7)" />
          <text>作废</text>
        </label>
      </radio-group>

      <view v-if="filteredGames.length === 0" class="empty dim">该分类下暂无记录</view>

      <block v-else>
        <view
          v-for="g in pagedGames"
          :key="g.id"
          class="game-card"
          :class="{ voided: g.status === GAME_STATUS.VOIDED }"
        >
          <view class="game-header">
            <text class="game-time">🕐 {{ formatTime(g.playedAt) }}</text>
            <text class="tag" :class="g.status === GAME_STATUS.VOIDED ? 'tag-info' : 'tag-success'">
              {{ g.status === GAME_STATUS.VOIDED ? '已作废' : '正常' }}
            </text>
          </view>
          <view class="game-players">
            <view v-for="p in g.players" :key="p.playerId" class="player-score">
              <text>{{ p.playerName }}</text>
              <text :class="scoreClass(p.score)">{{ sign(p.score) }}{{ p.score }}</text>
            </view>
          </view>
          <view v-if="g.note" class="game-note dim">📝 {{ g.note }}</view>
          <view class="game-actions">
            <block v-if="g.status === GAME_STATUS.VOIDED">
              <button class="btn btn-text" @tap="restoreGame(g)">恢复</button>
              <button class="btn btn-text btn-text-danger" @tap="permanentDelete(g)">永久删除</button>
            </block>
            <block v-else>
              <button class="btn btn-text btn-text-warning" @tap="voidGame(g)">作废</button>
            </block>
          </view>
        </view>

        <view v-if="totalPages > 1" class="pagination">
          <button class="btn btn-text" :disabled="currentPage <= 1" @tap="currentPage--">上一页</button>
          <text class="dim">{{ currentPage }} / {{ totalPages }}</text>
          <button class="btn btn-text" :disabled="currentPage >= totalPages" @tap="currentPage++">下一页</button>
        </view>
      </block>
    </block>
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

.count-bar {
  margin-bottom: 16rpx;
}
.count-strong {
  color: var(--text-primary);
  font-weight: bold;
}

.empty {
  text-align: center;
  padding: 80rpx 0;
}

.status-filter {
  display: flex;
  gap: 32rpx;
  margin-bottom: 20rpx;
}
.filter-item {
  display: flex;
  align-items: center;
  gap: 4rpx;
  font-size: 28rpx;
}

.game-card {
  background: var(--bg-card);
  border-left: 6rpx solid var(--primary);
  border-radius: 16rpx;
  padding: 24rpx;
  margin-bottom: 20rpx;
}
.game-card.voided {
  border-left-color: var(--warning);
  opacity: 0.6;
}
.game-card.voided .game-players {
  text-decoration: line-through;
}
.game-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16rpx;
}
.game-time {
  font-size: 24rpx;
  color: var(--text-secondary);
}
.game-players {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
  margin-bottom: 12rpx;
}
.player-score {
  display: flex;
  justify-content: space-between;
  padding: 12rpx 20rpx;
  background: var(--bg-fill);
  border-radius: 10rpx;
  font-size: 28rpx;
}
.game-note {
  font-size: 24rpx;
  margin-bottom: 12rpx;
  font-style: italic;
}
.game-actions {
  display: flex;
  gap: 16rpx;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 32rpx;
  padding: 16rpx 0;
}
</style>
