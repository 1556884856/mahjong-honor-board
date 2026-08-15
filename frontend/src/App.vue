<script setup>
import { ref, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import { api } from './api.js'
import RecordTab from './components/RecordTab.vue'
import HistoryTab from './components/HistoryTab.vue'
import StatsTab from './components/StatsTab.vue'

const THEME_KEY = 'mahjong-theme'
const THEMES = [
  { value: 'light', label: '亮色' },
  { value: 'dark', label: '深色' },
  { value: 'green', label: '麻将绿' },
]

const currentTab = ref('record')
const players = ref([])
const games = ref([])
const loading = ref(false)

const theme = ref('dark')
const themeLabel = computed(() =>
  THEMES.find(t => t.value === theme.value)?.label || '深色'
)

function applyTheme(t) {
  theme.value = t
  const root = document.documentElement
  if (t === 'light') {
    root.classList.remove('dark')
    root.classList.remove('theme-green')
  } else {
    root.classList.add('dark')
    root.classList.toggle('theme-green', t === 'green')
  }
  localStorage.setItem(THEME_KEY, t)
}

function initTheme() {
  const saved = localStorage.getItem(THEME_KEY)
  applyTheme(saved && THEMES.some(t => t.value === saved) ? saved : 'dark')
}

async function refreshPlayers() {
  try {
    players.value = await api.getPlayers()
  } catch (e) {
    ElMessage.error(e.message || '加载玩家失败')
  }
}

async function refreshGames() {
  try {
    games.value = await api.getGames()
  } catch (e) {
    ElMessage.error(e.message || '加载对局失败')
  }
}

async function refreshAll() {
  loading.value = true
  try {
    await Promise.all([refreshPlayers(), refreshGames()])
  } catch (e) {
    ElMessage.error(e.message || '加载失败')
  } finally {
    loading.value = false
  }
}

function showToast(msg, type = 'info') {
  ElMessage({
    message: msg,
    type,
    customClass: 'app-toast',
    duration: 2500,
    showClose: false,
  })
}

function showConfirm(message, callback) {
  ElMessageBox.confirm(message, '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning',
  })
    .then(async () => {
      try {
        await callback()
      } catch (e) {
        ElMessage.error(e.message || '操作失败')
      }
    })
    .catch(() => {})
}

onMounted(() => {
  initTheme()
  refreshAll()
})
</script>

<template>
  <el-config-provider :locale="zhCn">
    <div v-loading="loading" element-loading-text="加载中..." class="app-wrap">
    <header class="app-header">
      <h1>🀄 麻将荣誉榜</h1>
      <div class="header-right">
        <el-dropdown trigger="click" @command="applyTheme">
          <el-button text>
            主题：{{ themeLabel }}
            <el-icon class="el-icon--right"><ArrowDown /></el-icon>
          </el-button>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item
                v-for="t in THEMES"
                :key="t.value"
                :command="t.value"
                :disabled="t.value === theme"
              >{{ t.label }}</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>
    </header>

    <el-tabs v-model="currentTab" class="app-tabs">
      <el-tab-pane label="记录对局" name="record">
        <RecordTab
          :players="players"
          @refresh-players="refreshPlayers"
          @game-created="refreshGames"
          @toast="showToast"
        />
      </el-tab-pane>
      <el-tab-pane label="历史记录" name="history">
        <HistoryTab
          :games="games"
          @refresh-games="refreshGames"
          @toast="showToast"
          @confirm="showConfirm"
        />
      </el-tab-pane>
      <el-tab-pane label="统计分析" name="stats">
        <StatsTab
          :players="players"
          :games="games"
          @refresh-games="refreshGames"
          @toast="showToast"
        />
      </el-tab-pane>
    </el-tabs>
    </div>
  </el-config-provider>
</template>

<style scoped>
.app-wrap {
  max-width: 900px;
  margin: 0 auto;
  min-height: 100vh;
}
.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 0 8px;
}
.app-header h1 {
  font-size: 24px;
  color: var(--el-color-primary);
}
.app-tabs :deep(.el-tabs__header) {
  margin-bottom: 16px;
}
</style>
