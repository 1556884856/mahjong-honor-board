<script setup>
import { ref, onMounted } from 'vue'
import { api } from './api.js'
import RecordTab from './components/RecordTab.vue'
import HistoryTab from './components/HistoryTab.vue'
import StatsTab from './components/StatsTab.vue'

const currentTab = ref('record')
const players = ref([])
const games = ref([])
const loading = ref(false)

const toastMsg = ref('')
const confirmState = ref(null) // { message, callback }
let toastTimer = null

async function refreshPlayers() {
  try {
    players.value = await api.getPlayers()
  } catch (e) {
    showToast(e.message || '加载玩家失败')
  }
}

async function refreshGames() {
  try {
    games.value = await api.getGames()
  } catch (e) {
    showToast(e.message || '加载对局失败')
  }
}

async function refreshAll() {
  loading.value = true
  try {
    await Promise.all([refreshPlayers(), refreshGames()])
  } catch (e) {
    showToast(e.message || '加载失败')
  } finally {
    loading.value = false
  }
}

function switchTab(tab) {
  currentTab.value = tab
}

function showToast(msg) {
  if (toastTimer) clearTimeout(toastTimer)
  toastMsg.value = msg
  toastTimer = setTimeout(() => { toastMsg.value = '' }, 2500)
}

function showConfirm(message, callback) {
  confirmState.value = { message, callback }
}

function closeConfirm() {
  confirmState.value = null
}

async function onConfirm() {
  const callback = confirmState.value?.callback
  if (!callback) {
    closeConfirm()
    return
  }

  try {
    await callback()
  } catch (e) {
    showToast(e.message || '操作失败')
  } finally {
    closeConfirm()
  }
}

onMounted(refreshAll)
</script>

<template>
  <header class="app-header">
    <h1>🀄 麻将荣誉榜</h1>
    <nav class="tabs">
      <button :class="{ active: currentTab === 'record' }" @click="switchTab('record')">记录对局</button>
      <button :class="{ active: currentTab === 'history' }" @click="switchTab('history')">历史记录</button>
      <button :class="{ active: currentTab === 'stats' }" @click="switchTab('stats')">统计分析</button>
    </nav>
  </header>

  <main>
    <RecordTab
      v-if="currentTab === 'record'"
      :players="players"
      @refresh-players="refreshPlayers"
      @game-created="refreshGames"
      @toast="showToast"
    />
    <HistoryTab
      v-else-if="currentTab === 'history'"
      :games="games"
      @refresh-games="refreshGames"
      @toast="showToast"
      @confirm="showConfirm"
    />
    <StatsTab
      v-else
      :players="players"
      :games="games"
      @refresh-games="refreshGames"
      @toast="showToast"
    />
  </main>

  <div v-if="loading" class="loading-overlay">加载中...</div>
  <div v-if="toastMsg" class="toast">{{ toastMsg }}</div>

  <div v-if="confirmState" class="confirm-overlay" @click.self="closeConfirm">
    <div class="confirm-dialog">
      <p>{{ confirmState.message }}</p>
      <div class="actions">
        <button class="btn btn-ghost" @click="closeConfirm">取消</button>
        <button class="btn btn-danger" @click="onConfirm">确定</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.app-header {
  text-align: center;
  margin-bottom: 20px;
}
.app-header h1 {
  font-size: 24px;
  color: var(--accent);
  margin-bottom: 12px;
}
.tabs {
  display: flex;
  gap: 4px;
  background: var(--bg-card);
  border-radius: var(--radius);
  padding: 4px;
}
.tabs button {
  flex: 1;
  padding: 10px;
  border: none;
  background: transparent;
  color: var(--text-dim);
  font-size: 14px;
  cursor: pointer;
  border-radius: 8px;
  transition: all 0.2s;
}
.tabs button.active {
  background: var(--accent);
  color: #000;
  font-weight: 600;
}
.tabs button:hover:not(.active) {
  background: var(--bg-hover);
  color: var(--text);
}

.loading-overlay {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.35);
  color: var(--accent);
  font-weight: 600;
  z-index: 1500;
}
</style>
