<script>
export default {
  onLaunch() {
    console.log('麻将荣誉榜小程序启动')
  },
}
</script>

<style>
/* ===== 全局基础样式 ===== */
page {
  background: #0f1e1a;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
  line-height: 1.6;
  color: #e0e0e0;
}

/* 页面容器：铺满屏、内边距 */
.page-wrap {
  min-height: 100vh;
  padding: 24rpx;
  box-sizing: border-box;
}

/* ===== 主题变量（挂在页面根 view 上，向下继承） ===== */
/* 麻将绿（默认） */
.theme-green {
  --bg-page: #0f1e1a;
  --bg-card: #1a2e28;
  --bg-fill: #243a32;
  --primary: #e8c547;
  --primary-text: #3a2f00;
  --text-primary: #e0e0e0;
  --text-secondary: #888888;
  --border: #2a4a3e;
  --success: #4caf50;
  --warning: #ff9800;
  --danger: #ef5350;
  background: var(--bg-page);
  color: var(--text-primary);
}

/* 深色 */
.theme-dark {
  --bg-page: #141414;
  --bg-card: #1e1f20;
  --bg-fill: #2a2b2c;
  --primary: #409eff;
  --primary-text: #ffffff;
  --text-primary: #e5eaf3;
  --text-secondary: #a3a6ad;
  --border: #3a3b3d;
  --success: #67c23a;
  --warning: #e6a23c;
  --danger: #f56c6c;
  background: var(--bg-page);
  color: var(--text-primary);
}

/* 亮色 */
.theme-light {
  --bg-page: #f5f7fa;
  --bg-card: #ffffff;
  --bg-fill: #f0f2f5;
  --primary: #409eff;
  --primary-text: #ffffff;
  --text-primary: #303133;
  --text-secondary: #909399;
  --border: #dcdfe6;
  --success: #67c23a;
  --warning: #e6a23c;
  --danger: #f56c6c;
  background: var(--bg-page);
  color: var(--text-primary);
}

/* ===== 通用卡片 ===== */
.card {
  background: var(--bg-card);
  border-radius: 16rpx;
  padding: 24rpx;
  margin-bottom: 24rpx;
}
.card-title {
  font-size: 30rpx;
  font-weight: bold;
  color: var(--primary);
  margin-bottom: 20rpx;
}

/* ===== 通用按钮 ===== */
.btn {
  border-radius: 12rpx;
  font-size: 28rpx;
  padding: 0 32rpx;
  height: 72rpx;
  line-height: 72rpx;
  text-align: center;
  border: none;
}
.btn::after { border: none; }
.btn-primary {
  background: var(--primary);
  color: var(--primary-text);
}
.btn-text {
  background: transparent;
  color: var(--primary);
  font-size: 26rpx;
  padding: 0 12rpx;
  height: 56rpx;
  line-height: 56rpx;
}
.btn-text-danger { color: var(--danger); }
.btn-text-warning { color: var(--warning); }
.btn-block { width: 100%; }

/* 次要文字 */
.dim { color: var(--text-secondary); font-size: 26rpx; }

/* 得分颜色 */
.score-positive { color: var(--success); }
.score-negative { color: var(--danger); }
.score-zero { color: var(--text-secondary); }

/* 状态标签 */
.tag {
  display: inline-block;
  font-size: 22rpx;
  padding: 2rpx 16rpx;
  border-radius: 8rpx;
  line-height: 1.6;
}
.tag-success { color: var(--success); background: rgba(76, 175, 80, 0.15); }
.tag-info { color: var(--text-secondary); background: rgba(136, 136, 136, 0.15); }
</style>
