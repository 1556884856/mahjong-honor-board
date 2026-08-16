<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { api } from '../api.js'

const props = defineProps({
  games: { type: Array, default: () => [] },
  players: { type: Array, default: () => [] },
})
const emit = defineEmits(['refresh-games', 'toast', 'confirm'])
const GAME_STATUS = { ACTIVE: 0, VOIDED: 1 }

const statusFilter = ref('all') // all | active | voided
const currentPage = ref(1)

// 编辑对局
const editVisible = ref(false)
const editGame = ref(null)
const editSelectedIds = ref([])
const editScores = ref({})
const editNote = ref('')
const editPlayedAt = ref(null)
const editSubmitting = ref(false)

function toDate(s) {
  const m = /^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2}):(\d{2})/.exec(String(s))
  if (!m) return new Date(0)
  return new Date(+m[1], +m[2] - 1, +m[3], +m[4], +m[5], +m[6])
}

const sortedGames = computed(() =>
  [...props.games].sort((a, b) => toDate(b.playedAt) - toDate(a.playedAt))
)

const activeCount = computed(() => sortedGames.value.filter(g => g.status === GAME_STATUS.ACTIVE).length)
const voidedCount = computed(() => sortedGames.value.filter(g => g.status === GAME_STATUS.VOIDED).length)

const filteredGames = computed(() => {
  if (statusFilter.value === 'active') {
    return sortedGames.value.filter(g => g.status === GAME_STATUS.ACTIVE)
  }
  if (statusFilter.value === 'voided') {
    return sortedGames.value.filter(g => g.status === GAME_STATUS.VOIDED)
  }
  return sortedGames.value
})

// 每页条数：根据窗口高度动态计算
function computePageSize() {
  const h = window.innerHeight
  const n = Math.floor((h - 380) / 130)
  return Math.max(5, Math.min(20, n))
}
const pageSize = ref(computePageSize())

const totalPages = computed(() =>
  Math.max(1, Math.ceil(filteredGames.value.length / pageSize.value))
)

const pagedGames = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  return filteredGames.value.slice(start, start + pageSize.value)
})

watch(statusFilter, () => { currentPage.value = 1 })
watch(() => filteredGames.value.length, () => {
  if (currentPage.value > totalPages.value) currentPage.value = totalPages.value
})

function onResize() {
  pageSize.value = computePageSize()
  if (currentPage.value > totalPages.value) currentPage.value = totalPages.value
}
onMounted(() => window.addEventListener('resize', onResize))
onBeforeUnmount(() => window.removeEventListener('resize', onResize))

function formatTime(iso) {
  const d = toDate(iso)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

function scoreClass(score) {
  return score > 0 ? 'score-positive' : score < 0 ? 'score-negative' : 'score-zero'
}
function sign(score) { return score > 0 ? '+' : '' }

async function voidGame(game) {
  try {
    await api.updateGameStatus(game.id, GAME_STATUS.VOIDED)
    emit('refresh-games')
    emit('toast', '已作废', 'success')
  } catch (e) {
    emit('toast', e.message, 'error')
  }
}

async function restoreGame(game) {
  try {
    await api.updateGameStatus(game.id, GAME_STATUS.ACTIVE)
    emit('refresh-games')
    emit('toast', '已恢复', 'success')
  } catch (e) {
    emit('toast', e.message, 'error')
  }
}

function permanentDelete(game) {
  emit('confirm', '永久删除后无法恢复，确定删除？', async () => {
    await api.deleteGame(game.id)
    emit('refresh-games')
    emit('toast', '已永久删除', 'success')
  })
}

function fmtLocal(d) {
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

function openEdit(game) {
  editGame.value = game
  editSelectedIds.value = game.players.map(p => p.playerId)
  editScores.value = {}
  game.players.forEach(p => { editScores.value[p.playerId] = p.score })
  editNote.value = game.note || ''
  editPlayedAt.value = toDate(game.playedAt)
  editVisible.value = true
}

async function saveEdit() {
  if (editSelectedIds.value.length < 2) {
    emit('toast', '至少需要2名玩家', 'warning')
    return
  }
  if (editSelectedIds.value.length > 4) {
    emit('toast', '每局最多只能选择4名玩家', 'warning')
    return
  }
  editSubmitting.value = true
  try {
    const players = editSelectedIds.value.map(id => ({
      playerId: id,
      score: Number(editScores.value[id] ?? 0),
    }))
    const editDate = editPlayedAt.value ? new Date(editPlayedAt.value) : new Date()
    if (Number.isNaN(editDate.getTime())) {
      emit('toast', '对局时间无效', 'warning')
      return
    }
    const playedAtStr = fmtLocal(editDate)
    await api.updateGame(editGame.value.id, {
      playedAt: playedAtStr,
      note: editNote.value.trim() || null,
      players,
    })
    editVisible.value = false
    emit('refresh-games')
    emit('toast', '对局已更新', 'success')
  } catch (e) {
    emit('toast', e.message, 'error')
  } finally {
    editSubmitting.value = false
  }
}
</script>

<template>
  <div>
    <div class="action-bar">
      <span class="count">
        共 <strong>{{ sortedGames.length }}</strong> 条记录
        （{{ activeCount }} 条正常，{{ voidedCount }} 条已作废）
      </span>
    </div>

    <el-empty v-if="sortedGames.length === 0" description="还没有对局记录，去「记录对局」开始第一局吧" />

    <template v-else>
      <el-radio-group v-model="statusFilter" class="status-filter">
        <el-radio-button value="all">全部</el-radio-button>
        <el-radio-button value="active">正常</el-radio-button>
        <el-radio-button value="voided">作废</el-radio-button>
      </el-radio-group>

      <el-empty v-if="filteredGames.length === 0" description="该分类下暂无记录" />

      <template v-else>
        <el-card
          v-for="g in pagedGames"
          :key="g.id"
          class="game-card"
          :class="{ voided: g.status === GAME_STATUS.VOIDED }"
          shadow="hover"
        >
          <div class="game-header">
            <span class="game-time">🕐 {{ formatTime(g.playedAt) }}</span>
            <el-tag :type="g.status === GAME_STATUS.VOIDED ? 'info' : 'success'" size="small" effect="light">
              {{ g.status === GAME_STATUS.VOIDED ? '已作废' : '正常' }}
            </el-tag>
          </div>
          <div class="game-players">
            <div v-for="p in g.players" :key="p.playerId" class="player-score">
              <span>{{ p.playerName }}</span>
              <span :class="scoreClass(p.score)">{{ sign(p.score) }}{{ p.score }}</span>
            </div>
          </div>
          <div v-if="g.note" class="game-note">📝 {{ g.note }}</div>
          <div class="game-actions">
            <template v-if="g.status === GAME_STATUS.VOIDED">
              <el-button text type="primary" size="small" @click="restoreGame(g)">恢复</el-button>
              <el-button text type="danger" size="small" @click="permanentDelete(g)">永久删除</el-button>
            </template>
            <template v-else>
              <el-button text type="primary" size="small" @click="openEdit(g)">编辑</el-button>
              <el-button text type="warning" size="small" @click="voidGame(g)">作废</el-button>
            </template>
          </div>
        </el-card>

        <el-pagination
          v-if="totalPages > 1"
          v-model:current-page="currentPage"
          class="pagination"
          :page-size="pageSize"
          :total="filteredGames.length"
          layout="prev, pager, next, total"
          background
          small
        />
      </template>
    </template>

    <el-dialog v-model="editVisible" title="编辑对局" width="560px" append-to-body>
      <p class="select-label">选择参与本局对局的玩家：</p>
      <el-checkbox-group v-model="editSelectedIds" class="player-select" :max="4">
        <el-checkbox-button v-for="p in players" :key="p.id" :value="p.id">
          {{ p.name }}
        </el-checkbox-button>
      </el-checkbox-group>

      <div v-if="editSelectedIds.length > 0" class="score-area">
        <div v-for="id in editSelectedIds" :key="id" class="score-row">
          <label>{{ players.find(p => p.id === id)?.name }}</label>
          <el-input-number v-model="editScores[id]" :controls="false" :step="1" placeholder="0" />
          <span>分</span>
        </div>
      </div>

      <div class="field-row">
        <label class="field-label">对局时间</label>
        <el-date-picker
          v-model="editPlayedAt"
          type="datetime"
          placeholder="选择对局时间"
          format="YYYY-MM-DD HH:mm"
        />
      </div>

      <div class="field-row">
        <el-input v-model="editNote" placeholder="备注（可选）" />
      </div>

      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="primary" :loading="editSubmitting" @click="saveEdit">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.action-bar { margin-bottom: 12px; }
.count { font-size: 14px; color: var(--el-text-color-secondary); }
.count strong { color: var(--el-text-color-primary); }
.status-filter { margin-bottom: 12px; }
.game-card {
  margin-bottom: 12px;
  border-left: 3px solid var(--el-color-primary);
}
.game-card.voided {
  border-left-color: var(--el-color-warning);
  opacity: 0.6;
}
.game-card.voided .game-players { text-decoration: line-through; }
.game-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px; }
.game-time { font-size: 13px; color: var(--el-text-color-secondary); }
.game-players {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 8px;
  margin-bottom: 10px;
}
.player-score {
  display: flex;
  justify-content: space-between;
  padding: 6px 10px;
  background: var(--el-fill-color-light);
  border-radius: 6px;
  font-size: 14px;
}
.score-positive { color: var(--el-color-success); }
.score-negative { color: var(--el-color-danger); }
.score-zero { color: var(--el-text-color-secondary); }
.game-note { font-size: 12px; color: var(--el-text-color-secondary); margin-bottom: 8px; font-style: italic; }
.game-actions { display: flex; gap: 8px; }
.pagination { margin-top: 8px; justify-content: center; }

/* 编辑对局弹窗 */
.select-label { font-size: 14px; margin-bottom: 8px; }
.player-select { display: flex; flex-wrap: wrap; gap: 8px; margin: 12px 0; }
.score-area { margin-top: 8px; }
.score-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.score-row label { min-width: 80px; font-size: 14px; }
.score-row :deep(.el-input-number) { width: 140px; }
.field-row { display: flex; align-items: center; gap: 12px; margin-top: 12px; }
.field-label { min-width: 60px; font-size: 14px; }
.field-row :deep(.el-date-picker),
.field-row :deep(.el-input) { flex: 1; }
</style>
