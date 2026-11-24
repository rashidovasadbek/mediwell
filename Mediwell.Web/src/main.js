import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { setUnauthorizedHandler } from './api/client'
import { logout } from './auth'

import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'

const app = createApp(App)

// Element Plus ikonkalarini ro'yxatga olish
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
    app.component(key, component)
}

// Token eskirgan yoki yaroqsiz — chiqib, login sahifasiga
setUnauthorizedHandler(() => {
    logout()
    const current = router.currentRoute.value
    if (current.path !== '/login')
        router.replace({ path: '/login', query: { redirect: current.fullPath } })
})

app.use(router)
app.use(ElementPlus)
app.mount('#app')