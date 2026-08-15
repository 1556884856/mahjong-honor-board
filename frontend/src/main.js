import { createApp } from 'vue'
import {
  ElButton,
  ElCard,
  ElCheckbox,
  ElCheckboxButton,
  ElCheckboxGroup,
  ElConfigProvider,
  ElDatePicker,
  ElDropdown,
  ElDropdownItem,
  ElDropdownMenu,
  ElEmpty,
  ElIcon,
  ElInput,
  ElInputNumber,
  ElLoading,
  ElOption,
  ElPagination,
  ElRadioButton,
  ElRadioGroup,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTabs,
  ElTabPane,
  ElTag,
} from 'element-plus'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import { ArrowDown } from '@element-plus/icons-vue'
import App from './App.vue'
import './style.css'

const app = createApp(App)

const components = [
  ElButton,
  ElCard,
  ElCheckbox,
  ElCheckboxButton,
  ElCheckboxGroup,
  ElConfigProvider,
  ElDatePicker,
  ElDropdown,
  ElDropdownItem,
  ElDropdownMenu,
  ElEmpty,
  ElIcon,
  ElInput,
  ElInputNumber,
  ElOption,
  ElPagination,
  ElRadioButton,
  ElRadioGroup,
  ElSelect,
  ElTable,
  ElTableColumn,
  ElTabs,
  ElTabPane,
  ElTag,
]

components.forEach(component => {
  if (component.name) app.component(component.name, component)
})

app.directive('loading', ElLoading.directive)
app.component('ArrowDown', ArrowDown)
app.mount('#app')
