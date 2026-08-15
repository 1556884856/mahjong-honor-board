// 通用 UI 辅助：toast 与确认框（对应网页端的 ElMessage / ElMessageBox）
export function toast(msg, type = 'none') {
  const iconMap = {
    success: 'success',
    error: 'error',
    warning: 'none',
    info: 'none',
  }
  uni.showToast({
    title: msg || '操作完成',
    icon: iconMap[type] || 'none',
    duration: 2500,
  })
}

export function confirm(message, callback) {
  return new Promise((resolve) => {
    uni.showModal({
      title: '提示',
      content: message,
      confirmText: '确定',
      cancelText: '取消',
      success: async (res) => {
        if (res.confirm) {
          try {
            await callback()
          } catch (e) {
            toast(e.message || '操作失败', 'error')
          }
        }
        resolve(res.confirm)
      },
      fail: () => resolve(false),
    })
  })
}
