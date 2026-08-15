<script setup>
import { ref, computed, watch } from 'vue'
import { api } from '../api.js'

const props = defineProps({ players: { type: Array, default: () => [] } })
const emit = defineEmits(['refresh-players', 'game-created', 'toast'])

const newPlayerName = ref('')
const selectedPlayerIds = ref([])
const scores = ref({}) // { playerId: score }
const note = ref('')
const playedAt = ref(new Date())
const submitting = ref(false)

const totalSum = computed(() => {
  return selectedPlayerIds.value.reduce((sum, id) => {
    const v = Number(scores.value[id] ?? 0)
    return sum + (Number.isNaN(v) ? 0 : v)
  }, 0)
})

watch(selectedPlayerIds, (ids) => {
  const idSet = new Set(ids.map(Number))
  for (const k of Object.keys(scores.value)) {
    if (!idSet.has(Number(k))) delete scores.value[k]
  }
}, { deep: true })

async function addPlayer() {
  const name = newPlayerName.value.trim()
  if (!name) return
  try {
    await api.createPlayer(name)
    newPlayerName.value = ''
    emit('refresh-players')
    emit('toast', '玩家已添加', 'success')
  } catch (e) {
    emit('toast', e.message, 'error')
  }
}

async function removePlayer(player) {
  try {
    await api.deletePlayer(player.id)
    const idx = selectedPlayerIds.value.indexOf(player.id)
    if (idx >= 0) selectedPlayerIds.value.splice(idx, 1)
    delete scores.value[player.id]
    emit('refresh-players')
    emit('toast', '玩家已删除', 'success')
  } catch (e) {
    emit('toast', e.message, 'error')
  }
}

function fmtLocal(d) {
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

async function submitGame() {
  if (submitting.value) return

  if (selectedPlayerIds.value.length < 2) {
    emit('toast', '至少需要选择2名玩家', 'warning')
    return
  }

  if (selectedPlayerIds.value.length > 4) {
    emit('toast', '每局最多只能选择4名玩家', 'warning')
    return
  }

  submitting.value = true
  try {
    const players = selectedPlayerIds.value.map(id => {
      const score = Number(scores.value[id] ?? 0)
      if (Number.isNaN(score)) {
        throw new Error('得分无效')
      }
      return { playerId: id, score }
    })

    const playedAtStr = playedAt.value ? fmtLocal(new Date(playedAt.value)) : fmtLocal(new Date())
    await api.createGame({
      playedAt: playedAtStr,
      note: note.value.trim() || null,
      players,
    })
    selectedPlayerIds.value = []
    scores.value = {}
    note.value = ''
    playedAt.value = new Date()
    emit('game-created')
    emit('toast', '对局已记录', 'success')
  } catch (e) {
    emit('toast', e.message, 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div>
    <el-card class="block-card">
      <template #header>玩家管理</template>
      <div class="add-player-row">
        <el-input
          v-model="newPlayerName"
          placeholder="输入玩家名字，回车添加"
          @keyup.enter="addPlayer"
        />
        <el-button type="primary" @click="addPlayer">+ 添加</el-button>
      </div>
      <div v-if="players.length === 0" class="dim">还没有玩家，先添加几个吧</div>
      <div v-else class="player-chips">
        <el-tag
          v-for="p in players"
          :key="p.id"
          closable
          type="info"
          effect="dark"
          @close="removePlayer(p)"
        >{{ p.name }}</el-tag>
      </div>
    </el-card>

    <el-card class="block-card">
      <template #header>新建对局</template>
      <p v-if="players.length === 0" class="dim">请先在上方添加玩家</p>

      <template v-else>
        <p class="select-label">选择参与本局对局的玩家：</p>
        <el-checkbox-group v-model="selectedPlayerIds" class="player-select" :max="4">
          <el-checkbox-button v-for="p in players" :key="p.id" :value="p.id">
            {{ p.name }}
          </el-checkbox-button>
        </el-checkbox-group>

        <div v-if="selectedPlayerIds.length > 0" class="score-area">
          <div v-for="id in selectedPlayerIds" :key="id" class="score-row">
            <label>{{ players.find(p => p.id === id)?.name }}</label>
            <el-input-number v-model="scores[id]" :controls="false" :step="1" placeholder="0" />
            <span>分</span>
          </div>
          <div class="score-sum" :class="totalSum === 0 ? 'zero' : 'non-zero'">
            总分合计: {{ totalSum }} {{ totalSum === 0 ? '✓' : '(麻将一般为0)' }}
          </div>
        </div>

        <div class="field-row">
          <label class="field-label">对局时间</label>
          <el-date-picker
            v-model="playedAt"
            type="datetime"
            placeholder="选择对局时间"
            format="YYYY-MM-DD HH:mm"
          />
        </div>

        <div class="field-row">
          <el-input v-model="note" placeholder="备注（可选）" />
        </div>

        <el-button
          type="primary"
          class="submit-btn"
          :loading="submitting"
          @click="submitGame"
        >记录对局</el-button>
      </template>
    </el-card>
  </div>
</template>

<style scoped>
.block-card { margin-bottom: 16px; }
.add-player-row { display: flex; gap: 8px; }
.player-chips { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 12px; }
.dim { color: var(--el-text-color-secondary); font-size: 14px; }
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
.score-sum { margin-top: 12px; font-size: 13px; color: var(--el-text-color-secondary); text-align: right; }
.score-sum.zero { color: var(--el-color-success); }
.score-sum.non-zero { color: var(--el-color-warning); }
.field-row { display: flex; align-items: center; gap: 12px; margin-top: 12px; }
.field-label { min-width: 60px; font-size: 14px; }
.field-row :deep(.el-date-picker),
.field-row :deep(.el-input) { flex: 1; }
.submit-btn { width: 100%; margin-top: 16px; }
</style>
