<script setup>
import { ref } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { api, getBaseUrl, setBaseUrl, resetBaseUrl } from '../../utils/api.js'
import { toast } from '../../utils/ui.js'
import { getTheme } from '../../utils/theme.js'

const theme = ref(getTheme())
const baseUrl = ref('')
const testing = ref(false)

onShow(() => {
  theme.value = getTheme()
  baseUrl.value = getBaseUrl()
})

// 保存服务器地址
function onSave() {
  const url = baseUrl.value.trim().replace(/\/+$/, '')
  if (!/^https?:\/\/.+/.test(url)) {
    toast('地址需以 http:// 或 https:// 开头', 'warning')
    return
  }
  setBaseUrl(url)
  toast('已保存，立即生效', 'success')
}

// 测试连接：拉取玩家列表验证
async function onTest() {
  if (testing.value) return
  const url = baseUrl.value.trim().replace(/\/+$/, '')
  if (!/^https?:\/\/.+/.test(url)) {
    toast('地址需以 http:// 或 https:// 开头', 'warning')
    return
  }
  setBaseUrl(url)
  baseUrl.value = url
  testing.value = true
  try {
    const players = await api.getPlayers()
    toast(`连接成功，共 ${players.length} 名玩家`, 'success')
  } catch (e) {
    toast(`连接失败：${e.message}`, 'error')
  } finally {
    testing.value = false
  }
}

function onReset() {
  resetBaseUrl()
  baseUrl.value = getBaseUrl()
  toast('已恢复默认地址', 'success')
}
</script>

<template>
  <view class="page-wrap" :class="'theme-' + theme">
    <!-- 服务器地址 -->
    <view class="card">
      <view class="card-title">服务器地址</view>
      <view class="dim desc">小程序请求的后端接口地址，改完立即生效。若改用新地址后数据没变，请确认地址正确。</view>
      <input
        v-model="baseUrl"
        class="input"
        placeholder="http://47.85.163.218/api"
        placeholder-class="dim"
      />
      <view class="btn-row">
        <button class="btn btn-primary" @tap="onSave">保存</button>
        <button class="btn" :disabled="testing" @tap="onTest">{{ testing ? '测试中...' : '测试连接' }}</button>
        <button class="btn btn-danger" @tap="onReset">恢复默认</button>
      </view>
    </view>

    <!-- 使用说明 -->
    <view class="card">
      <view class="card-title">体验版使用提示</view>
      <view class="tips">
        <view class="tip-item">· 手机打开小程序后，点右上角「... → 开发调试」，否则微信会拦截未备案的 HTTP 地址。</view>
        <view class="tip-item">· 正式发布前，把服务器地址换成已备案域名的 https 地址（如 https://api.example.com/api）。</view>
        <view class="tip-item">· 本机默认地址：http://47.85.163.218/api（阿里云 Nginx 80 端口反向代理）。</view>
      </view>
    </view>

    <!-- 关于 -->
    <view class="card">
      <view class="card-title">关于</view>
      <view class="about-row"><text class="about-label">AppID</text><text class="about-value">wxd95ea6e47a94d42f</text></view>
      <view class="about-row"><text class="about-label">版本</text><text class="about-value">1.0.0</text></view>
    </view>
  </view>
</template>

<style scoped>
.page-wrap {
  padding: 24rpx;
}
.card {
  background: var(--bg-card);
  border: 1rpx solid var(--border);
  border-radius: 20rpx;
  padding: 28rpx;
  margin-bottom: 24rpx;
}
.card-title {
  font-size: 30rpx;
  font-weight: bold;
  color: var(--text-primary);
  margin-bottom: 16rpx;
}
.desc {
  font-size: 24rpx;
  margin-bottom: 16rpx;
}
.input {
  height: 76rpx;
  line-height: 76rpx;
  background: var(--bg-fill);
  border-radius: 12rpx;
  padding: 0 20rpx;
  font-size: 28rpx;
  color: var(--text-primary);
  margin-bottom: 20rpx;
}
.btn-row {
  display: flex;
  gap: 16rpx;
}
.btn {
  flex: 1;
  height: 72rpx;
  line-height: 72rpx;
  text-align: center;
  font-size: 28rpx;
  border-radius: 12rpx;
  background: var(--bg-fill);
  color: var(--text-primary);
  padding: 0;
  margin: 0;
}
.btn::after { border: none; }
.btn-primary {
  background: var(--primary);
  color: var(--primary-text);
}
.btn-danger {
  color: var(--danger);
}
.tips {
  display: flex;
  flex-direction: column;
  gap: 12rpx;
}
.tip-item {
  font-size: 24rpx;
  color: var(--text-secondary);
  line-height: 1.6;
}
.about-row {
  display: flex;
  justify-content: space-between;
  padding: 12rpx 0;
  font-size: 26rpx;
}
.about-label {
  color: var(--text-secondary);
}
.about-value {
  color: var(--text-primary);
}
</style>
