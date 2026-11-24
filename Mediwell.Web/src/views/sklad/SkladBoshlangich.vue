<script setup>
import { ref, computed, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { groupId, currentGroup } from '../../sklad/useGroup'
import { api, fmt, fmtDate, errText, today } from '../../sklad/skladApi'

const rows = ref([])            // { productId, name, nal, mediwell }
const date = ref(today())
const loading = ref(false)
const saving = ref(false)
const existing = ref(null)      // { date, docNo, count } — guruhda opening bor bo'lsa
const search = ref('')

async function load() {
    if (!groupId.value) return
    loading.value = true
    try {
        const [products, opening] = await Promise.all([
            api.products(groupId.value, true),
            api.movements({ groupId: groupId.value, type: 'opening', pageSize: 1000 }),
        ])

        // Mavjud opening qiymatlari bilan to'ldiramiz
        const map = {}
        for (const m of opening.items) {
            const v = (map[m.productId] ||= { nal: 0, mediwell: 0 })
            v[m.toWarehouse] += m.qty
        }
        rows.value = products
            .filter(p => p.active || map[p.id])
            .map(p => ({
                productId: p.id,
                name: p.name,
                active: p.active,
                nal: map[p.id]?.nal ?? 0,
                mediwell: map[p.id]?.mediwell ?? 0,
            }))

        if (opening.total > 0) {
            const first = opening.items[0]
            existing.value = { date: first.movementDate, docNo: first.docNo, count: opening.total }
            date.value = first.movementDate
        } else {
            existing.value = null
            date.value = today()
        }
    } catch (e) {
        ElMessage.error(errText(e))
    } finally {
        loading.value = false
    }
}

watch(groupId, load, { immediate: true })

const filtered = computed(() => {
    const s = search.value.trim().toLowerCase()
    return s ? rows.value.filter(r => r.name.toLowerCase().includes(s)) : rows.value
})

function summary({ columns }) {
    return columns.map((col, i) => {
        if (i === 0) return 'ЖАМИ'
        if (col.property === 'nal') return fmt(rows.value.reduce((s, r) => s + (r.nal || 0), 0))
        if (col.property === 'mediwell') return fmt(rows.value.reduce((s, r) => s + (r.mediwell || 0), 0))
        if (col.property === 'total') return fmt(rows.value.reduce((s, r) => s + (r.nal || 0) + (r.mediwell || 0), 0))
        return ''
    })
}

async function save() {
    if (!date.value) return ElMessage.warning('Sana tanlanmagan')
    if (existing.value) {
        try {
            await ElMessageBox.confirm(
                `Bu guruhda ${fmtDate(existing.value.date)} sanali boshlang'ich qoldiq allaqachon bor ` +
                `(${existing.value.docNo || ''}). Eski qiymatlar o'chirilib, yangilari yoziladi. Davom etasizmi?`,
                'Boshlang\'ich qoldiqni almashtirish',
                { confirmButtonText: 'Almashtirish', cancelButtonText: 'Bekor qilish', type: 'warning' })
        } catch {
            return
        }
    }

    saving.value = true
    try {
        const res = await api.saveOpening({
            groupId: groupId.value,
            movementDate: date.value,
            lines: rows.value.map(r => ({ productId: r.productId, nal: r.nal || 0, mediwell: r.mediwell || 0 })),
        })
        ElMessage.success(res.docNo
            ? `Saqlandi: ${res.docNo} (${res.inserted} qator)`
            : "Saqlandi: boshlang'ich qoldiq bo'sh")
        load()
    } catch (e) {
        ElMessage({ type: 'error', message: errText(e), duration: 8000, showClose: true })
    } finally {
        saving.value = false
    }
}
</script>

<template>
    <div>
        <h1 class="title">Boshlang'ich qoldiq — {{ currentGroup?.name }}</h1>
        <p class="sub">Ma'lum sanadagi qoldiq: har dori uchun нал va mediwell</p>

        <el-alert v-if="existing" type="warning" show-icon :closable="false" style="margin-bottom: 16px"
                  :title="`Bu guruhda ${fmtDate(existing.date)} sanali boshlang'ich qoldiq allaqachon kiritilgan (${existing.docNo || ''}).`"
                  description="Quyida mavjud qiymatlar ko'rsatilgan. Saqlasangiz, eski boshlang'ich qoldiq to'liq almashtiriladi." />

        <div class="toolbar">
            <span>Sana:</span>
            <el-date-picker v-model="date" type="date" value-format="YYYY-MM-DD" format="DD.MM.YYYY"
                            :clearable="false" style="width: 160px" />
            <el-input v-model="search" placeholder="Dori qidirish" clearable style="width: 240px">
                <template #prefix><el-icon><Search /></el-icon></template>
            </el-input>
            <div style="flex: 1" />
            <el-button type="primary" size="large" :loading="saving" :disabled="rows.length === 0" @click="save">
                Saqlash
            </el-button>
        </div>

        <el-table :data="filtered" v-loading="loading" border stripe show-summary :summary-method="summary"
                  empty-text="Bu guruhda dori yo'q — avval «Dorilar» sahifasida qo'shing" style="width: 100%">
            <el-table-column label="Препарат" prop="name" min-width="260" align="center">
                <template #default="{ row }">
                    {{ row.name }}
                    <el-tag v-if="!row.active" size="small" type="info" style="margin-left: 6px">faolsiz</el-tag>
                </template>
            </el-table-column>
            <el-table-column label="нал" prop="nal" width="180" align="center">
                <template #default="{ row }">
                    <el-input-number v-model="row.nal" :min="0" :step="1" step-strictly :controls="false"
                                     style="width: 140px" />
                </template>
            </el-table-column>
            <el-table-column label="mediwell" prop="mediwell" width="180" align="center">
                <template #default="{ row }">
                    <el-input-number v-model="row.mediwell" :min="0" :step="1" step-strictly :controls="false"
                                     style="width: 140px" />
                </template>
            </el-table-column>
            <el-table-column label="Жами" prop="total" width="160" align="center">
                <template #default="{ row }"><b class="num">{{ fmt((row.nal || 0) + (row.mediwell || 0)) }}</b></template>
            </el-table-column>
        </el-table>
    </div>
</template>
