<script setup>
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { groupId, currentGroup } from '../../sklad/useGroup'
import { api, fmt, errText } from '../../sklad/skladApi'

const rows = ref([])
const loading = ref(false)
const exporting = ref(false)
const asOf = ref(null)          // bo'sh = bugungacha (hamma harakatlar)
const search = ref('')
const hideZero = ref(false)

async function load() {
    if (!groupId.value) return
    loading.value = true
    try {
        rows.value = await api.balance(groupId.value, asOf.value)
    } catch (e) {
        ElMessage.error(errText(e))
    } finally {
        loading.value = false
    }
}

watch([groupId, asOf], load, { immediate: true })

const filtered = computed(() => {
    const s = search.value.trim().toLowerCase()
    return rows.value.filter(r =>
        (!s || r.productName.toLowerCase().includes(s)) &&
        (!hideZero.value || r.nal !== 0 || r.mediwell !== 0))
})

function summary({ columns }) {
    return columns.map((col, i) => {
        if (i === 0) return 'ЖАМИ'
        const key = col.property
        if (['nal', 'mediwell', 'total'].includes(key))
            return fmt(filtered.value.reduce((s, r) => s + r[key], 0))
        return ''
    })
}

async function exportExcel() {
    exporting.value = true
    try {
        await api.exportBalance(groupId.value, asOf.value)
    } catch (e) {
        ElMessage.error('Excel\'ga yuklab bo\'lmadi: ' + errText(e))
    } finally {
        exporting.value = false
    }
}
</script>

<template>
    <div>
        <h1 class="title">Qoldiq — {{ currentGroup?.name }}</h1>
        <p class="sub">Har dori bo'yicha нал va mediwell omborlaridagi qoldiq</p>

        <div class="toolbar">
            <el-date-picker v-model="asOf" type="date" value-format="YYYY-MM-DD" format="DD.MM.YYYY"
                            placeholder="Sana (bo'sh — hozirgi)" clearable style="width: 200px" />
            <el-input v-model="search" placeholder="Dori qidirish" clearable style="width: 240px">
                <template #prefix><el-icon><Search /></el-icon></template>
            </el-input>
            <el-checkbox v-model="hideZero">0 qoldiqlarni yashirish</el-checkbox>
            <div style="flex: 1" />
            <el-button type="success" :loading="exporting" @click="exportExcel">
                <el-icon style="margin-right: 6px"><Download /></el-icon> Excel'ga yuklash
            </el-button>
        </div>

        <el-table :data="filtered" v-loading="loading" border stripe show-summary :summary-method="summary"
                  empty-text="Dorilar yo'q" style="width: 100%">
            <el-table-column label="Препарат" prop="productName" align="center" min-width="260">
                <template #default="{ row }">
                    {{ row.productName }}
                    <el-tag v-if="!row.active" size="small" type="info" style="margin-left: 6px">faolsiz</el-tag>
                </template>
            </el-table-column>
            <el-table-column label="нал" prop="nal" align="center" width="160">
                <template #default="{ row }"><span class="num" :class="{ neg: row.nal < 0 }">{{ fmt(row.nal) }}</span></template>
            </el-table-column>
            <el-table-column label="mediwell" prop="mediwell" align="center" width="160">
                <template #default="{ row }"><span class="num" :class="{ neg: row.mediwell < 0 }">{{ fmt(row.mediwell) }}</span></template>
            </el-table-column>
            <el-table-column label="Жами" prop="total" align="center" width="160">
                <template #default="{ row }"><b class="num">{{ fmt(row.total) }}</b></template>
            </el-table-column>
        </el-table>
    </div>
</template>
