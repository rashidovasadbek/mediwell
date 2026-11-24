<script setup>
import { ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { groupId, currentGroup } from '../../sklad/useGroup'
import { api, fmt, errText } from '../../sklad/skladApi'

const rows = ref([])
const loading = ref(false)
const showInactive = ref(false)

const form = ref({ name: '', perechPrice: null, sortOrder: null })
const adding = ref(false)

async function load() {
    if (!groupId.value) return
    loading.value = true
    try {
        rows.value = await api.products(groupId.value, showInactive.value)
    } catch (e) {
        ElMessage.error(errText(e))
    } finally {
        loading.value = false
    }
}

watch([groupId, showInactive], load, { immediate: true })

async function add() {
    if (!form.value.name.trim()) return ElMessage.warning('Dori nomi kiritilmagan')
    adding.value = true
    try {
        const nextOrder = rows.value.reduce((m, r) => Math.max(m, r.sortOrder), 0) + 1
        await api.createProduct({
            groupId: groupId.value,
            name: form.value.name.trim(),
            perechPrice: form.value.perechPrice,
            sortOrder: form.value.sortOrder ?? nextOrder,
            active: true,
        })
        ElMessage.success("Dori qo'shildi")
        form.value = { name: '', perechPrice: null, sortOrder: null }
        load()
    } catch (e) {
        ElMessage.error(errText(e))
    } finally {
        adding.value = false
    }
}

// ---------- Tahrirlash ----------
const dialog = ref(false)
const saving = ref(false)
const edit = ref(null)

function openEdit(row) {
    edit.value = { ...row }
    dialog.value = true
}

async function save(row, okText = 'Saqlandi') {
    try {
        await api.updateProduct(row.id, {
            name: row.name.trim(),
            perechPrice: row.perechPrice,
            sortOrder: row.sortOrder ?? 0,
            active: row.active,
        })
        ElMessage.success(okText)
        load()
        return true
    } catch (e) {
        ElMessage.error(errText(e))
        return false
    }
}

async function saveEdit() {
    if (!edit.value.name.trim()) return ElMessage.warning('Dori nomi kiritilmagan')
    saving.value = true
    if (await save(edit.value)) dialog.value = false
    saving.value = false
}

async function toggleActive(row) {
    if (row.active) {
        try {
            await ElMessageBox.confirm(
                `«${row.name}» faolsizlantirilsinmi? U kiritish ro'yxatlarida ko'rinmaydi, tarix saqlanadi.`,
                'Faolsizlantirish', { confirmButtonText: 'Ha', cancelButtonText: 'Bekor qilish', type: 'warning' })
        } catch {
            return
        }
    }
    await save({ ...row, active: !row.active }, row.active ? 'Faolsizlantirildi' : 'Faollashtirildi')
}

async function remove(row) {
    try {
        await ElMessageBox.confirm(
            row.hasMovements
                ? `«${row.name}» bo'yicha harakatlar bor — u o'chirilmaydi, faqat faolsizlantiriladi. Davom etasizmi?`
                : `«${row.name}» butunlay o'chirilsinmi?`,
            "Rostdan o'chirmoqchimisiz?",
            { confirmButtonText: "O'chirish", cancelButtonText: 'Bekor qilish', type: 'warning' })
    } catch {
        return
    }
    try {
        const res = await api.deleteProduct(row.id)
        ElMessage({ type: res.deleted ? 'success' : 'warning', message: res.message })
        load()
    } catch (e) {
        ElMessage.error(errText(e))
    }
}
</script>

<template>
    <div>
        <h1 class="title">Dorilar — {{ currentGroup?.name }}</h1>
        <p class="sub">Guruh dorilari: qo'shish, nom / Переч narxi / tartibni tahrirlash, faolsizlantirish</p>

        <el-card style="margin-bottom: 20px">
            <el-form :inline="true" @submit.prevent="add">
                <el-form-item label="Nomi">
                    <el-input v-model="form.name" placeholder="Masalan: Пеноксидант 100 мл" style="width: 280px" />
                </el-form-item>
                <el-form-item label="Переч narxi">
                    <el-input-number v-model="form.perechPrice" :min="0" :controls="false" placeholder="Ixtiyoriy"
                                     style="width: 150px" />
                </el-form-item>
                <el-form-item label="Tartib">
                    <el-input-number v-model="form.sortOrder" :min="0" :controls="false" placeholder="Oxiriga"
                                     style="width: 100px" />
                </el-form-item>
                <el-form-item>
                    <el-button type="primary" native-type="submit" :loading="adding">Qo'shish</el-button>
                </el-form-item>
            </el-form>
        </el-card>

        <div class="toolbar">
            <el-checkbox v-model="showInactive">Faolsizlarini ham ko'rsatish</el-checkbox>
        </div>

        <el-table :data="rows" v-loading="loading" border stripe empty-text="Dorilar yo'q" style="width: 100%">
            <el-table-column label="Tartib" prop="sortOrder" width="90" align="center" />
            <el-table-column label="Nomi" prop="name" min-width="260" align="center" />
            <el-table-column label="Переч narxi" width="160" align="center">
                <template #default="{ row }"><span class="num">{{ fmt(row.perechPrice) }}</span></template>
            </el-table-column>
            <el-table-column label="Holat" width="120" align="center">
                <template #default="{ row }">
                    <el-tag :type="row.active ? 'success' : 'info'" size="small">{{ row.active ? 'faol' : 'faolsiz' }}</el-tag>
                </template>
            </el-table-column>
            <el-table-column label="" width="300" align="center">
                <template #default="{ row }">
                    <el-button size="small" @click="openEdit(row)">Tahrirlash</el-button>
                    <el-button size="small" @click="toggleActive(row)">
                        {{ row.active ? 'Faolsizlantirish' : 'Faollashtirish' }}
                    </el-button>
                    <el-button size="small" type="danger" plain @click="remove(row)">O'chirish</el-button>
                </template>
            </el-table-column>
        </el-table>

        <el-dialog v-model="dialog" title="Dorini tahrirlash" width="460px" :close-on-click-modal="false">
            <el-form v-if="edit" label-width="120px">
                <el-form-item label="Nomi"><el-input v-model="edit.name" /></el-form-item>
                <el-form-item label="Переч narxi">
                    <el-input-number v-model="edit.perechPrice" :min="0" :controls="false" />
                </el-form-item>
                <el-form-item label="Tartib">
                    <el-input-number v-model="edit.sortOrder" :min="0" :controls="false" />
                </el-form-item>
                <el-form-item label="Faol"><el-switch v-model="edit.active" /></el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="dialog = false">Bekor qilish</el-button>
                <el-button type="primary" :loading="saving" @click="saveEdit">Saqlash</el-button>
            </template>
        </el-dialog>
    </div>
</template>
