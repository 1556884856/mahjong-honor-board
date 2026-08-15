// 主题管理（与网页端三主题一致：light 亮色 / dark 深色 / green 麻将绿）
const KEY = 'mahjong-theme'

export const THEMES = [
  { value: 'green', label: '麻将绿' },
  { value: 'dark', label: '深色' },
  { value: 'light', label: '亮色' },
]

export function getTheme() {
  const t = uni.getStorageSync(KEY)
  return THEMES.some((x) => x.value === t) ? t : 'green'
}

export function setTheme(t) {
  uni.setStorageSync(KEY, t)
}

export function themeClass() {
  return 'theme-' + getTheme()
}

// 弹出主题选择
export function chooseTheme(onChange) {
  uni.showActionSheet({
    itemList: THEMES.map((t) => t.label),
    success: (res) => {
      const t = THEMES[res.tapIndex].value
      setTheme(t)
      if (onChange) onChange(t)
    },
  })
}
