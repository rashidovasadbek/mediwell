// JWT token va foydalanuvchi — localStorage'da (client.js va auth.js umumiy ishlatadi)
const STORAGE_KEY = 'mediwell_user'

export function readSession() {
    try {
        const raw = localStorage.getItem(STORAGE_KEY)
        if (!raw) return null
        const s = JSON.parse(raw)
        if (!s?.token || !s?.user?.role) return null
        if (s.expiresAt && new Date(s.expiresAt) <= new Date()) return null  // muddati o'tgan
        return s
    } catch {
        return null
    }
}

export function writeSession(session) {
    try {
        if (session) localStorage.setItem(STORAGE_KEY, JSON.stringify(session))
        else localStorage.removeItem(STORAGE_KEY)
    } catch { /* localStorage yopiq bo'lishi mumkin */ }
}

export function getToken() {
    return readSession()?.token ?? null
}
