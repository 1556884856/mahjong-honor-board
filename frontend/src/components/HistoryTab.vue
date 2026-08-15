<script setup>
import { computed } from 'vue'
import { api } from '../api.js'

const props = defineProps({ games: { type: Array, default: () => [] } })
const emit = defineEmits(['refresh-games', 'toast', 'confirm'])
const GAME_STATUS = { ACTIVE: 0, VOIDED: 1 }

const sortedGames = computed(() =>
  [...props.games].sort((a, b) => new Date(b.playedAt) - new Date(a.playedAt))
)

const activeCount = computed(() => sortedGames.value.filter(g => g.status === GAME_STATUS.ACTIVE).length)
const voidedCount = computed(() => sortedGames.value.filter(g => g.status === GAME_STATUS.VOIDED).length)

function formatTime(iso) {
  const d = new Date(iso)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function scoreClass(score) {
  return score > 0 ? 'score-positive' : score < 0 ? 'score-negative' : 'score-zero'
}
function sign(score) { return score > 0 ? '+' : '' }

async function voidGame(game) {
  try {
    await api.updateGameStatus(game.id, GAME_STATUS.VOIDED)
    emit('refresh-games')
    emit('toast', '已作废')
  } catch (e) {
    emit('toast', e.message)
  }
}

async function restoreGame(game) {
  try {
    await api.updateGameStatus(game.id, GAME_STATUS.ACTIVE)
    emit('refresh-games')
    emit('toast', '已恢复')
  } catch (e) {
    emit('toast', e.message)
  }
}

function permanentDelete(game) {
  emit('confirm', '永久删除后无法恢复，确定删除？', async () => {
    await api.deleteGame(game.id)
    emit('refresh-games')
    emit('toast', '已永久删除')
  })
}
</script>

<template>
  <div>
    <div class="action-bar">
      <div class="count">
        共 <strong>{{ sortedGames.length }}</strong> 条记录
        （{{ activeCount }} 条正常，{{ voidedCount }} 条已作废）
      </div>
    </div>

    <div v-if="sortedGames.length === 0" class="empty-state">
      <p>还没有对局记录</p>
      <p>去「记录对局」开始第一局吧</p>
    </div>

    <div
      v-for="g in sortedGames"
      :key="g.id"
      class="game-card"
      :class="{ voided: g.status === GAME_STATUS.VOIDED }"
    >
      <div class="game-header">
        <span class="game-time">🕐 {{ formatTime(g.playedAt) }}</span>
        <span class="badge" :class="g.status === GAME_STATUS.VOIDED ? 'badge-voided' : 'badge-active'">
          {{ g.status === GAME_STATUS.VOIDED ? '已作废' : '正常' }}
        </span>
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
          <button class="btn btn-ghost btn-sm" @click="restoreGame(g)">恢复</button>
          <button class="btn btn-danger btn-sm" @click="permanentDelete(g)">永久删除</button>
        </template>
        <template v-else>
          <button class="btn btn-warning btn-sm" @click="voidGame(g)">作废</button>
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped>
.action-bar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.count { font-size: 14px; color: var(--text-dim); }
.count strong { color: var(--text); }
.game-card {
  background: var(--bg-card);
  border-radius: var(--radius);
  padding: 16px;
  margin-bottom: 12px;
  border-left: 3px solid var(--accent);
}
.game-card.voided {
  border-left-color: var(--orange);
  opacity: 0.6;
}
.game-card.voided .game-players { text-decoration: line-through; }
.game-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px; }
.game-time { font-size: 13px; color: var(--text-dim); }
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
  background: var(--bg-hover);
  border-radius: 6px;
  font-size: 14px;
}
.game-note { font-size: 12px; color: var(--text-dim); margin-bottom: 8px; font-style: italic; }
.game-actions { display: flex; gap: 8px; }
@media (max-width: 600px) {
  .game-players { grid-template-columns: 1fr 1fr; }
}
</style>
