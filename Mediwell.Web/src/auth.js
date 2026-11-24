import { reactive, computed } from 'vue'
import client from './api/client'
import { readSession, writeSession } from './api/token'

// Login backend orqali: POST /auth/login → JWT token (muddati: Auth:TokenHours)
const state = reactive({
    session: readSession(),   // { token, expiresAt, user: { id, login, fullName, role } }
})

export const currentUser = computed(() => state.session?.user ?? null)
export const isLoggedIn = computed(() => state.session !== null)
export const role = computed(() => state.session?.user?.role ?? null)

// Rol uchun bosh sahifa
export function homeFor(r) {
    return r === 'admin' ? '/admin/kirim' : '/sklad/chiqim'
}

export async function login(username, password) {
    try {
        const { data } = await client.post('/auth/login', {
            login: String(username).trim(),
            password,
        })
        state.session = { token: data.token, expiresAt: data.expiresAt, user: data.user }
        writeSession(state.session)
        return { ok: true, user: data.user }
    } catch (e) {
        const status = e?.response?.status
        const error = status === 401 ? "Login yoki parol noto'g'ri"
            : status === 429 ? "Juda ko'p urinish. Bir daqiqadan keyin qayta urinib ko'ring"
            : e?.response ? `Server xatosi (${status})`
            : "Server bilan aloqa yo'q"
        return { ok: false, error }
    }
}

export function logout() {
    state.session = null
    writeSession(null)
}
