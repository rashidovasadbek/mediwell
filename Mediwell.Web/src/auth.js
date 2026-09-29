import { reactive, computed } from 'vue'

// DIQQAT: hozircha backendda auth yo'q — login faqat frontendda.
// Backend /auth/login tayyor bo'lganda shu faylni almashtirish kifoya.
const USERS = [
    { username: 'admin', password: 'admin123', role: 'admin', fullName: 'Administrator' },
    { username: 'sklad', password: 'sklad123', role: 'skladchi', fullName: 'Skladchi' },
]

const STORAGE_KEY = 'mediwell_user'

function readStored() {
    try {
        const raw = localStorage.getItem(STORAGE_KEY)
        if (!raw) return null
        const u = JSON.parse(raw)
        return u && u.role ? u : null
    } catch {
        return null
    }
}

const state = reactive({
    user: readStored(),
})

export const currentUser = computed(() => state.user)
export const isLoggedIn = computed(() => state.user !== null)
export const role = computed(() => state.user?.role ?? null)

// Rol uchun bosh sahifa
export function homeFor(r) {
    return r === 'admin' ? '/admin/kirim' : '/sklad/chiqim'
}

export function login(username, password) {
    const found = USERS.find(
        u => u.username === String(username).trim().toLowerCase() && u.password === password
    )
    if (!found) return { ok: false, error: 'Login yoki parol noto\'g\'ri' }

    const user = { username: found.username, role: found.role, fullName: found.fullName }
    state.user = user
    localStorage.setItem(STORAGE_KEY, JSON.stringify(user))
    return { ok: true, user }
}

export function logout() {
    state.user = null
    localStorage.removeItem(STORAGE_KEY)
}
