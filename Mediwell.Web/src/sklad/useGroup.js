// Tanlangan guruh — barcha "Sklad hisobi" sahifalari uchun umumiy, localStorage'da eslab qolinadi
import { ref, computed, watch } from 'vue'
import { api } from './skladApi'

const KEY = 'sklad_group_id'

function readStored() {
    try {
        const v = Number(localStorage.getItem(KEY))
        return v > 0 ? v : null
    } catch {
        return null
    }
}

export const groups = ref([])
export const groupId = ref(readStored())
export const currentGroup = computed(() => groups.value.find(g => g.id === groupId.value) || null)

watch(groupId, (v) => {
    try {
        if (v) localStorage.setItem(KEY, String(v))
    } catch { /* localStorage yopiq bo'lishi mumkin */ }
})

export async function loadGroups() {
    groups.value = (await api.groups()).filter(g => g.active)
    if (!groups.value.some(g => g.id === groupId.value))
        groupId.value = groups.value[0]?.id ?? null
}
