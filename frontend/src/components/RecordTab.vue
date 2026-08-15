<script setup>
import { ref, computed } from 'vue'
import { api } from '../api.js'

const props = defineProps({ players: { type: Array, default: () => [] } })
const emit = defineEmits(['refresh-players', 'game-created', 'toast'])

const newPlayerName = ref('')
const selectedPlayerIds = ref([])
const scores = ref({}) // { playerId: score }
const note = ref('')
const submitting = ref(false)

const totalSum = computed(() => {
  return selectedPlayerIds.value.reduce((sum, id) => {
    const v = Number(scores.value[id] ?? 0)
    return sum + (Number.isNaN(v) ? 0 : v)
  }, 0)
})

function togglePlayer(id) {
  const idx = selectedPlayerIds.value.indexOf(id)
  if (idx >= 0) {
    selectedPlayerIds.value.splice(idx, 1)
    delete scores.value[id]
  } else {
    selectedPlayerIds.value.push(id)
  }
}

async function addPlayer() {
  const name = newPlayerName.value.trim()
  if (!name) return
  try {
    await api.createPlayer(name)
    newPlayerName.value = ''
    emit('refresh-players')
    emit('toast', '玩家已添加')
  } catch (e) {
    emit('toast', e.message)
  }
}

async function removePlayer(player) {
  try {
    await api.deletePlayer(player.id)
    // 如果该玩家已被选中，移除
    const idx = selectedPlayerIds.value.indexOf(player.id)
    if (idx >= 0) {
      selectedPlayerIds.value.splice(idx, 1)
      delete scores.value[player.id]
    }
    emit('refresh-players')
    emit('toast', '玩家已删除')
  } catch (e) {
    emit('toast', e.message)
  }
}

async function submitGame() {
  if (submitting.value) return

  if (selectedPlayerIds.value.length < 2) {
    emit('toast', '至少需要选择2名玩家')
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

    await api.createGame({
      playedAt: new Date().toISOString(),
      note: note.value.trim() || null,
      players,
    })
    selectedPlayerIds.value = []
    scores.value = {}
    note.value = ''
    emit('game-created')
    emit('toast', '对局已记录')
  } catch (e) {
    emit('toast', e.message)
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div>
    <div class="card">
      <h2>玩家管理</h2>
      <div class="add-player-row">
        <input
          v-model="newPlayerName"
          type="text"
          placeholder="输入玩家名字，回车添加"
          @keydown.enter="addPlayer"
        >
        <button class="btn btn-primary" @click="addPlayer">+ 添加</button>
      </div>
      <div class="player-chips">
        <span v-if="players.length === 0" class="dim">还没有玩家，先添加几个吧</span>
        <span v-for="p in players" :key="p.id" class="player-chip">
          {{ p.name }}
          <span class="remove" @click="removePlayer(p)">&times;</span>
        </span>
      </div>
    </div>

    <div class="card">
      <h2>新建对局</h2>
      <p v-if="players.length === 0" class="dim">请先在上方添加玩家</p>
      <template v-else>
        <p class="select-label">选择参与本局对局的玩家：</p>
        <div class="player-select">
          <span
            v-for="p in players"
            :key="p.id"
            class="player-option"
            :class="{ selected: selectedPlayerIds.includes(p.id) }"
            @click="togglePlayer(p.id)"
          >{{ p.name }}</span>
        </div>

        <div v-if="selectedPlayerIds.length > 0" class="score-area">
          <div v-for="id in selectedPlayerIds" :key="id" class="score-row">
            <label>{{ players.find(p => p.id === id)?.name }}</label>
            <input v-model.number="scores[id]" type="number" placeholder="0" step="any">
            <span>分</span>
          </div>
          <div class="score-sum" :class="totalSum === 0 ? 'zero' : 'non-zero'">
            总分合计: {{ totalSum }} {{ totalSum === 0 ? '✓' : '(麻将一般为0)' }}
          </div>
        </div>

        <div class="note-row">
          <input v-model="note" type="text" placeholder="备注（可选）">
        </div>
        <button class="btn btn-primary btn-block" :disabled="submitting" @click="submitGame">
          记录对局
        </button>
      </template>
    </div>
  </div>
</template>

<style scoped>
.add-player-row { display: flex; gap: 8px; margin-bottom: 4px; }
.add-player-row input { flex: 1; }
.dim { color: var(--text-dim); font-size: 14px; }
.player-chips { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 12px; }
.player-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: var(--bg-hover);
  border: 1px solid var(--border);
  border-radius: 20px;
  padding: 6px 14px;
  font-size: 14px;
}
.player-chip .remove { cursor: pointer; color: var(--text-dim); font-size: 16px; }
.player-chip .remove:hover { color: var(--red); }
.select-label { font-size: 14px; margin-bottom: 8px; }
.player-select { display: flex; flex-wrap: wrap; gap: 8px; margin: 12px 0; }
.player-option {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: var(--bg-hover);
  border: 2px solid var(--border);
  border-radius: 8px;
  padding: 8px 14px;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 14px;
}
.player-option.selected {
  border-color: var(--accent);
  background: rgba(232, 197, 71, 0.1);
}
.player-option:hover { border-color: var(--accent-dim); }
.score-area { margin-top: 8px; }
.score-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 0;
  border-bottom: 1px solid var(--border);
}
.score-row label { min-width: 80px; font-size: 14px; }
.score-row input { width: 120px; text-align: right; }
.score-sum { margin-top: 12px; font-size: 13px; color: var(--text-dim); text-align: right; }
.score-sum.zero { color: var(--green); }
.score-sum.non-zero { color: var(--orange); }
.note-row { margin-top: 12px; }
.note-row input { width: 100%; }
.btn-block { width: 100%; margin-top: 12px; }
</style>
