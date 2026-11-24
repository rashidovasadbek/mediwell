// Sklad hisobi (markirovkasiz) — API chaqiruvlari va umumiy yordamchilar
import client from '../api/client'

// ---------- API ----------

export const api = {
    groups: () => client.get('/stock-groups').then(r => r.data),

    products: (groupId, includeInactive = false) =>
        client.get('/stock-products', { params: { groupId, includeInactive } }).then(r => r.data),
    createProduct: (body) => client.post('/stock-products', body).then(r => r.data),
    updateProduct: (id, body) => client.put(`/stock-products/${id}`, body).then(r => r.data),
    deleteProduct: (id) => client.delete(`/stock-products/${id}`).then(r => r.data),

    balance: (groupId, asOf) =>
        client.get('/stock-balance', { params: { groupId, asOf: asOf || undefined } }).then(r => r.data),

    async exportBalance(groupId, asOf) {
        const res = await client.get('/stock-balance/export', {
            params: { groupId, asOf: asOf || undefined },
            responseType: 'blob',
        })
        const cd = res.headers['content-disposition'] || ''
        const m = cd.match(/filename="?([^";]+)"?/)
        const name = m ? m[1] : 'Qoldiq.xlsx'
        const url = URL.createObjectURL(res.data)
        const a = document.createElement('a')
        a.href = url
        a.download = name
        a.click()
        URL.revokeObjectURL(url)
    },

    movements: (params) => client.get('/stock-movements', { params }).then(r => r.data),
    createBatch: (body) => client.post('/stock-movements/batch', body).then(r => r.data),
    saveOpening: (body) => client.post('/stock-movements/opening', body).then(r => r.data),
    updateMovement: (id, body) => client.put(`/stock-movements/${id}`, body).then(r => r.data),
    deleteMovement: (id) => client.delete(`/stock-movements/${id}`).then(r => r.data),
    suggestions: (field, groupId) =>
        client.get('/stock-movements/suggestions', { params: { field, groupId } }).then(r => r.data),
}

// ---------- Matnlar ----------

export const TYPE_LABELS = {
    opening: "Boshlang'ich qoldiq",
    receipt: 'Kirim',
    transfer: "Ko'chirish",
    write_off: 'Spisanie',
    sale_perech: 'Переч sotuv',
    sale_nal: 'Нал sotuv',
}

export const WAREHOUSES = [
    { value: 'nal', label: 'нал' },
    { value: 'mediwell', label: 'mediwell' },
]

export const whLabel = (w) => (w === 'nal' ? 'нал' : w === 'mediwell' ? 'mediwell' : '')

// ---------- Formatlash ----------

// 12089 → "12 089", 45000.5 → "45 000.50"
export function fmt(n) {
    if (n === null || n === undefined || n === '') return ''
    const x = Number(n)
    if (Number.isNaN(x)) return ''
    const [i, d] = Math.abs(x).toFixed(2).split('.')
    const s = i.replace(/\B(?=(\d{3})+(?!\d))/g, ' ')
    return (x < 0 ? '-' : '') + s + (d !== '00' ? '.' + d : '')
}

// "2026-11-25" → "25.11.2026"
export function fmtDate(s) {
    if (!s) return ''
    const [y, m, d] = String(s).slice(0, 10).split('-')
    return `${d}.${m}.${y}`
}

export function today() {
    const d = new Date()
    const p = (n) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

// Backend xatosini o'zbekcha matnga aylantirish
export function errText(e) {
    const data = e?.response?.data
    if (data) {
        if (Array.isArray(data.errors) && data.errors.length) return data.errors.join('\n')
        if (data.error) return data.error
    }
    if (e?.response?.status === 403) return "Bu amal uchun ruxsat yo'q"
    if (e?.response?.status) return `Server xatosi (${e.response.status})`
    return "Server bilan aloqa yo'q"
}

// el-autocomplete uchun: ro'yxatdan filtrlash
export function suggestFrom(listRef) {
    return (q, cb) => {
        const s = (q || '').toLowerCase()
        cb(listRef.value
            .filter(v => !s || v.toLowerCase().includes(s))
            .slice(0, 50)
            .map(value => ({ value })))
    }
}
