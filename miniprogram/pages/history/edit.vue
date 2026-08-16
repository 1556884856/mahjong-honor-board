<script setup>
import { ref, computed } from 'vue'
import { onLoad } from '@dcloudio/uni-app'
import { api } from '../../utils/api.js'
import { toast } from '../../utils/ui.js'
import { getTheme } from '../../utils/theme.js'

const gameId = ref(null)
const players = ref([])
const selectedPlayerIds = ref([])
const scores = ref({})
const note = ref('')
const playedAtDate = ref('')
const playedAtTime = ref('')
const submitting = ref(false)
const loading = ref(true)
const theme = ref(getTheme())

function pad(n) {
  return String(n).padStart(2, '0')
}

onLoad((options) => {
  gameId.value = Number(options.id)
  load()
})

async function load() {
  loading.value = true
  try {
    const [ps, g] = await Promise.all([api.getPlayers(), api.getGame(gameId.value)])
    players.value = ps
    selectedPlayerIds.value = g.players.map((p) => p.playerId)
    const sc = {}
    g.players.forEach((p) => {
      sc[p.playerId] = p.score
    })
    scores.value = sc
    note.value = g.note || ''

    // 拆分对局时间为日期 + 时间，供 picker 使用
    const m = /^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2}):(\d{2})/.exec(String(g.playedAt))
    if (m) {
      playedAtDate.value = `${m[1]}-${m[2]}-${m[3]}`
      playedAtTime.value = `${m[4]}:${m[5]}`
    }
  } catch (e) {
    toast(e.message, 'error')
  } finally {
    loading.value = false
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

function toggleSign(id) {
  const raw = String(scores.value[id] ?? '').trim()
  if (raw === '' || raw === '0') return
  scores.value[id] = raw.startsWith('-') ? raw.slice(1) : '-' + raw
}

function playerName(id) {
  return players.value.find((p) => p.id === id)?.name || ''
}

async function submitEdit() {
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

    const [yy, mo, dd] = playedAtDate.value.split('-').map(Number)
    const [hh, mi] = playedAtTime.value.split(':').map(Number)
    const localDate = new Date(yy, mo - 1, dd, hh, mi, 0)
    const playedAtStr = `${localDate.getFullYear()}-${pad(localDate.getMonth() + 1)}-${pad(localDate.getDate())} ${pad(localDate.getHours())}:${pad(localDate.getMinutes())}:${pad(localDate.getSeconds())}`

    await api.updateGame(gameId.value, {
      playedAt: playedAtStr,
      note: note.value.trim() || null,
      players: gamePlayers,
    })
    toast('对局已更新', 'success')
    setTimeout(() => uni.navigateBack(), 400)
  } catch (e) {
    toast(e.message, 'error')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <view class="page-wrap" :class="'theme-' + theme">
    <view class="card">
      <view class="card-title">编辑对局</view>
      <view v-if="loading" class="dim">加载中...</view>

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

        <button class="btn btn-primary btn-block submit-btn" :disabled="submitting" @tap="submitEdit">
          {{ submitting ? '保存中...' : '保存修改' }}
        </button>
      </block>
    </view>
  </view>
</template>

<style scoped>
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
