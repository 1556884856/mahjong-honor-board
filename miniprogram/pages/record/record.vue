<script setup>
import { ref, computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { api } from '../../utils/api.js'
import { toast, confirm } from '../../utils/ui.js'
import { THEMES, getTheme, chooseTheme } from '../../utils/theme.js'

const players = ref([])
const newPlayerName = ref('')
const selectedPlayerIds = ref([]) // number[]
const scores = ref({}) // { playerId: score }
const note = ref('')
const playedAtDate = ref('')
const playedAtTime = ref('')
const submitting = ref(false)
const theme = ref(getTheme())

// 编辑玩家
const editPlayerVisible = ref(false)
const editPlayerId = ref(null)
const editPlayerName = ref('')
const nameHistory = ref([])

const themeLabel = computed(
  () => THEMES.find((t) => t.value === theme.value)?.label || '麻将绿'
)

function pad(n) {
  return String(n).padStart(2, '0')
}
function fmtLocal(d) {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

function initNow() {
  const d = new Date()
  playedAtDate.value = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
  playedAtTime.value = `${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function onTheme() {
  chooseTheme((t) => {
    theme.value = t
  })
}

function onSettings() {
  uni.navigateTo({ url: '/pages/settings/settings' })
}

onShow(() => {
  theme.value = getTheme()
  if (!playedAtDate.value && !playedAtTime.value) initNow()
  loadPlayers()
})

async function loadPlayers() {
  try {
    players.value = await api.getPlayers()
  } catch (e) {
    toast(e.message, 'error')
  }
}

const totalSum = computed(() => {
  return selectedPlayerIds.value.reduce((sum, id) => {
    const v = Number(scores.value[id] ?? 0)
    return sum + (Number.isNaN(v) ? 0 : v)
  }, 0)
})

function onPlayerSelect(e) {
  selectedPlayerIds.value = e.detail.value.map(Number)
}

// 切换正负号：数字键盘没有负号键，用按钮翻转
function toggleSign(id) {
  const raw = String(scores.value[id] ?? '').trim()
  if (raw === '' || raw === '0') return
  scores.value[id] = raw.startsWith('-') ? raw.slice(1) : '-' + raw
}

async function addPlayer() {
  const name = newPlayerName.value.trim()
  if (!name) {
    toast('请输入玩家名字', 'warning')
    return
  }
  try {
    await api.createPlayer(name)
    newPlayerName.value = ''
    await loadPlayers()
    toast('玩家已添加', 'success')
  } catch (e) {
    toast(e.message, 'error')
  }
}

function removePlayer(player) {
  confirm(`删除玩家「${player.name}」？`, async () => {
    try {
      await api.deletePlayer(player.id)
      const idx = selectedPlayerIds.value.indexOf(player.id)
      if (idx >= 0) selectedPlayerIds.value.splice(idx, 1)
      delete scores.value[player.id]
      await loadPlayers()
      toast('玩家已删除', 'success')
    } catch (e) {
      toast(e.message, 'error')
    }
  })
}

function openEditPlayer(p) {
  editPlayerId.value = p.id
  editPlayerName.value = p.name
  nameHistory.value = []
  editPlayerVisible.value = true
  api
    .getPlayerNameHistory(p.id)
    .then((h) => {
      nameHistory.value = h || []
    })
    .catch(() => {})
}

async function saveEditPlayer() {
  const name = editPlayerName.value.trim()
  if (!name) {
    toast('请输入玩家名字', 'warning')
    return
  }
  try {
    await api.updatePlayer(editPlayerId.value, name)
    editPlayerVisible.value = false
    await loadPlayers()
    toast('玩家已重命名', 'success')
  } catch (e) {
    toast(e.message, 'error')
  }
}

function playerName(id) {
  return players.value.find((p) => p.id === id)?.name || ''
}

async function submitGame() {
  if (submitting.value) return

  if (selectedPlayerIds.value.length < 2) {
    toast('至少需要选择2名玩家', 'warning')
    return
  }
  if (selectedPlayerIds.value.length > 4) {
    toast('每局最多只能选择4名玩家', 'warning')
    return
  }

  submitting.value = true
  try {
    const gamePlayers = selectedPlayerIds.value.map((id) => {
      const score = Number(scores.value[id] ?? 0)
      if (Number.isNaN(score)) throw new Error('得分无效')
      return { playerId: id, score }
    })

    // iOS 的 JavaScriptCore 不支持 new Date('YYYY-MM-DDTHH:mm:ss') 字符串，
    // 会得到 Invalid Date 导致保存成当前时间。这里手动拆分构造。
    const [yy, mo, dd] = playedAtDate.value.split('-').map(Number)
    const [hh, mi] = playedAtTime.value.split(':').map(Number)
    const localDate = new Date(yy, mo - 1, dd, hh, mi, 0)
    const playedAtStr = fmtLocal(localDate)
    await api.createGame({
      playedAt: playedAtStr,
      note: note.value.trim() || null,
      players: gamePlayers,
    })
    selectedPlayerIds.value = []
    scores.value = {}
    note.value = ''
    initNow()
    toast('对局已记录', 'success')
  } catch (e) {
    toast(e.message, 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <view class="page-wrap" :class="'theme-' + theme">
    <!-- 顶部栏 + 主题切换 + 设置 -->
    <view class="top-bar">
      <text class="top-title">🀄 麻将荣誉榜</text>
      <view class="top-actions">
        <button class="btn btn-text" @tap="onTheme">主题：{{ themeLabel }}</button>
        <button class="btn btn-text" @tap="onSettings">⚙ 设置</button>
      </view>
    </view>

    <!-- 玩家管理 -->
    <view class="card">
      <view class="card-title">玩家管理</view>
      <view class="add-row">
        <input
          v-model="newPlayerName"
          class="input"
          placeholder="输入玩家名字"
          placeholder-class="dim"
          confirm-type="done"
          @confirm="addPlayer"
        />
        <button class="btn btn-primary" @tap="addPlayer">+ 添加</button>
      </view>
      <view v-if="players.length === 0" class="dim" style="margin-top: 16rpx">还没有玩家，先添加几个吧</view>
      <view v-else class="chips">
        <view v-for="p in players" :key="p.id" class="chip">
          <text class="chip-name">{{ p.name }}</text>
          <text class="chip-edit" @tap="openEditPlayer(p)">✎</text>
          <text class="chip-close" @tap="removePlayer(p)">×</text>
        </view>
      </view>
    </view>

    <!-- 新建对局 -->
    <view class="card">
      <view class="card-title">新建对局</view>
      <view v-if="players.length === 0" class="dim">请先在上方添加玩家</view>

      <block v-else>
        <view class="select-label">选择参与本局对局的玩家：</view>
        <checkbox-group class="player-select" @change="onPlayerSelect">
          <label v-for="p in players" :key="p.id" class="player-check">
            <checkbox
              :value="String(p.id)"
              :checked="selectedPlayerIds.includes(p.id)"
              :disabled="selectedPlayerIds.length >= 4 && !selectedPlayerIds.includes(p.id)"
              :color="'#e8c547'"
              style="transform: scale(0.8)"
            />
            <text class="check-name">{{ p.name }}</text>
          </label>
        </checkbox-group>

        <view v-if="selectedPlayerIds.length > 0" class="score-area">
          <view v-for="id in selectedPlayerIds" :key="id" class="score-row">
            <text class="score-name">{{ playerName(id) }}</text>
            <input
              v-model="scores[id]"
              class="input score-input"
              type="digit"
              placeholder="0"
              placeholder-class="dim"
            />
            <view
              class="sign-btn"
              :class="String(scores[id] ?? '').startsWith('-') ? 'sign-negative' : ''"
              @tap="toggleSign(id)"
            >{{ String(scores[id] ?? '').startsWith('-') ? '−' : '+' }}</view>
            <text class="dim">分</text>
          </view>
          <view class="score-sum" :class="totalSum === 0 ? 'sum-zero' : 'sum-nonzero'">
            总分合计: {{ totalSum }} {{ totalSum === 0 ? '✓' : '(麻将一般为0)' }}
          </view>
        </view>

        <view class="field-row">
          <text class="field-label">对局时间</text>
          <view class="picker-group">
            <picker mode="date" :value="playedAtDate" @change="(e) => (playedAtDate = e.detail.value)">
              <view class="picker-box">{{ playedAtDate }}</view>
            </picker>
            <picker mode="time" :value="playedAtTime" @change="(e) => (playedAtTime = e.detail.value)">
              <view class="picker-box">{{ playedAtTime }}</view>
            </picker>
          </view>
        </view>

        <view class="field-row">
          <text class="field-label">备注</text>
          <input
            v-model="note"
            class="input"
            placeholder="备注（可选）"
            placeholder-class="dim"
          />
        </view>

        <button class="btn btn-primary btn-block submit-btn" :disabled="submitting" @tap="submitGame">
          {{ submitting ? '提交中...' : '记录对局' }}
        </button>
      </block>
    </view>

    <!-- 编辑玩家弹窗 -->
    <view v-if="editPlayerVisible" class="popup-mask" @tap="editPlayerVisible = false">
      <view class="popup-panel" @tap.stop>
        <view class="popup-title">编辑玩家</view>
        <input
          v-model="editPlayerName"
          class="input"
          placeholder="玩家名字"
          placeholder-class="dim"
          confirm-type="done"
          @confirm="saveEditPlayer"
        />
        <view v-if="nameHistory.length > 0" class="history-box">
          <view class="history-title">改名历史</view>
          <view v-for="h in nameHistory" :key="h.id" class="history-item">
            <text class="history-names">{{ h.oldName }} → {{ h.newName }}</text>
            <text class="history-time">{{ h.changedAt }}</text>
          </view>
        </view>
        <view class="popup-actions">
          <button class="btn btn-text" @tap="editPlayerVisible = false">取消</button>
          <button class="btn btn-primary" @tap="saveEditPlayer">保存</button>
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
.top-actions {
  display: flex;
  gap: 12rpx;
}
.top-title {
  font-size: 36rpx;
  font-weight: bold;
  color: var(--primary);
}

.add-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
}
.input {
  flex: 1;
  height: 72rpx;
  line-height: 72rpx;
  background: var(--bg-fill);
  border-radius: 12rpx;
  padding: 0 20rpx;
  font-size: 28rpx;
  color: var(--text-primary);
}

.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx;
  margin-top: 20rpx;
}
.chip {
  display: flex;
  align-items: center;
  gap: 8rpx;
  background: var(--bg-fill);
  border: 1rpx solid var(--border);
  border-radius: 999rpx;
  padding: 8rpx 24rpx;
  font-size: 26rpx;
}
.chip-edit {
  color: var(--text-secondary);
  font-size: 28rpx;
  padding: 0 4rpx;
}
.chip-close {
  color: var(--text-secondary);
  font-size: 30rpx;
  padding: 0 4rpx;
}

/* 编辑玩家弹窗 */
.popup-mask {
  position: fixed;
  left: 0;
  top: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.55);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 999;
}
.popup-panel {
  width: 600rpx;
  background: var(--bg-card);
  border-radius: 20rpx;
  padding: 32rpx;
}
.popup-title {
  font-size: 32rpx;
  font-weight: bold;
  margin-bottom: 24rpx;
}
.history-box {
  margin-top: 24rpx;
  border-top: 1rpx dashed var(--border);
  padding-top: 16rpx;
  max-height: 360rpx;
  overflow-y: auto;
}
.history-title {
  font-size: 24rpx;
  color: var(--text-secondary);
  margin-bottom: 8rpx;
}
.history-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8rpx 0;
  font-size: 26rpx;
}
.history-names {
  color: var(--text-primary);
}
.history-time {
  color: var(--text-secondary);
  font-size: 22rpx;
}
.popup-actions {
  display: flex;
  justify-content: flex-end;
  gap: 20rpx;
  margin-top: 28rpx;
}

.select-label {
  font-size: 26rpx;
  margin-bottom: 12rpx;
}
.player-select {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx;
  margin: 16rpx 0;
}
.player-check {
  display: flex;
  align-items: center;
  gap: 8rpx;
  background: var(--bg-fill);
  border-radius: 10rpx;
  padding: 12rpx 20rpx;
}
.check-name {
  font-size: 26rpx;
}

.score-area {
  margin-top: 12rpx;
}
.score-row {
  display: flex;
  align-items: center;
  gap: 20rpx;
  padding: 18rpx 0;
  border-bottom: 1rpx solid var(--border);
}
.score-name {
  width: 140rpx;
  font-size: 28rpx;
}
.score-input {
  flex: 1;
  text-align: right;
}
.sign-btn {
  width: 64rpx;
  height: 64rpx;
  line-height: 64rpx;
  text-align: center;
  border-radius: 50%;
  background: var(--bg-fill);
  border: 1rpx solid var(--border);
  font-size: 32rpx;
  color: var(--text-secondary);
}
.sign-negative {
  color: var(--warning);
  border-color: var(--warning);
}
.score-sum {
  margin-top: 16rpx;
  font-size: 24rpx;
  text-align: right;
}
.sum-zero { color: var(--success); }
.sum-nonzero { color: var(--warning); }

.field-row {
  display: flex;
  align-items: center;
  gap: 20rpx;
  margin-top: 24rpx;
}
.field-label {
  width: 140rpx;
  font-size: 28rpx;
}
.picker-group {
  flex: 1;
  display: flex;
  gap: 16rpx;
}
.picker-box {
  flex: 1;
  height: 72rpx;
  line-height: 72rpx;
  background: var(--bg-fill);
  border-radius: 12rpx;
  padding: 0 20rpx;
  font-size: 28rpx;
  text-align: center;
}

.submit-btn {
  margin-top: 32rpx;
}
</style>
