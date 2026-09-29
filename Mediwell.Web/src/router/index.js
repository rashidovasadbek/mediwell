import { createRouter, createWebHistory } from 'vue-router'

import Login from '../views/Login.vue'
import AdminLayout from '../layouts/AdminLayout.vue'
import SkladLayout from '../layouts/SkladLayout.vue'

import Kirim from '../views/Kirim.vue'
import Qoldiq from '../views/Qoldiq.vue'
import Preparatlar from '../views/Preparatlar.vue'
import Chiqim from '../views/Chiqim.vue'

import Kodlar from '../views/Kodlar.vue'
import { isLoggedIn, role, homeFor } from '../auth'
import MediwellPrixod from '../views/MediwellPrixod.vue'

const routes = [
    { path: '/', redirect: () => (isLoggedIn.value ? homeFor(role.value) : '/login') },

    { path: '/login', name: 'Login', component: Login, meta: { public: true } },
    { path: '/kodlar', name: 'Kodlar', component: Kodlar },

    // ADMIN paneli
    {
        path: '/admin',
        component: AdminLayout,
        meta: { role: 'admin' },
        children: [
            { path: '', redirect: '/admin/kirim' },
            { path: 'kirim', name: 'Kirim', component: Kirim },
            { path: 'qoldiq', name: 'Qoldiq', component: Qoldiq },
            { path: 'preparatlar', name: 'Preparatlar', component: Preparatlar },
            { path: 'mediwell-prixod', name: 'MediwellPrixod', component: MediwellPrixod },
        ],
    },

    // SKLAD paneli
    {
        path: '/sklad',
        component: SkladLayout,
        meta: { role: 'skladchi' },
        children: [
            { path: '', redirect: '/sklad/chiqim' },
            { path: 'chiqim', name: 'Chiqim', component: Chiqim },
        ],
    },

    // Qolgan hamma narsa — rolga qarab yo'naltiriladi
    { path: '/:pathMatch(.*)*', redirect: '/' },
]

const router = createRouter({
    history: createWebHistory(),
    routes,
})

router.beforeEach((to) => {
    const needed = to.matched.find(r => r.meta.role)?.meta.role

    // Login qilmagan — login sahifasiga
    if (!isLoggedIn.value) {
        return to.meta.public ? true : { path: '/login', query: { redirect: to.fullPath } }
    }

    // Login qilgan bo'lsa login sahifasiga qaytmasin
    if (to.meta.public) return homeFor(role.value)

    // Boshqa rolning paneliga kirmoqchi — o'z paneliga qaytariladi
    if (needed && needed !== role.value) return homeFor(role.value)

    return true
})

export default router
