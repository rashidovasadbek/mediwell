<script setup>
import { ref, computed, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { groupId, currentGroup } from '../../sklad/useGroup'
import { api, fmt, fmtDate, errText, whLabel, suggestFrom, TYPE_LABELS, WAREHOUSES } from '../../sklad/skladApi'

const items = ref([])
const total = ref(0)
const loading = ref(false)
const products = ref([])   // faolsizlari ham (filtr va tahrir uchun)

const filters = ref({ range: null, type: '', productId: null, search: '' })
const page = ref(1)
const pageSize = ref(50)

const TYPE_TAG = {
    opening: 'info', receipt: 'success', transfer: 'warning',
    write_off: 'danger', sale_perech: 'primary', sale_nal: 'primary',
}

async function load() {
    if (!groupId.value) return
    loading.value = true
    try {
        const f = filters.value
        const res = await api.movements({
            groupId: groupId.value,
            type: f.type || undefined,
            productId: f.productId || undefined,
            from: f.range?.[0],
            to: f.range?.[1],
            search: f.search.trim() || undefined,
            page: page.value,
            pageSize: pageSize.value,
        })
        items.value = res.items
        total.value = res.total
    } catch (e) {
        ElMessage.error(errText(e))
    } finally {
        loading.value = false
    }
}

async function loadProducts() {
    try {
        products.value = await api.products(groupId.value, true)
    } catch (e) {
        ElMessage.error(errText(e))
    }
}

watch(groupId, () => {
    if (!groupId.value) return
    filters.value.productId = null
    page.value = 1
    loadProducts()
    load()
}, { immediate: true })

// Filtr o'zgarsa — 1-sahifadan (qidiruv uchun kichik kechikish)
let timer = null
watch(() => ({ ...filters.value }), () => {
    clearTimeout(timer)
    timer = setTimeout(() => { page.value = 1; load() }, 300)
})

function direction(row) {
    const f = whLabel(row.fromWarehouse)
    const t = whLabel(row.toWarehouse)
    if (f && t) return `${f} → ${t}`
    if (t) return `→ ${t}`
    return `${f} →`
}

// ---------- O'chirish ----------
async function remove(row) {
    try {
        await ElMessageBox.confirm(
            `${fmtDate(row.movementDate)} · ${TYPE_LABELS[row.movementType]} · ${row.productName} · ${fmt(row.qty)} ta`,
            "Rostdan o'chirmoqchimisiz?",
            { confirmButtonText: "O'chirish", cancelButtonText: 'Bekor qilish', type: 'warning' })
    } catch {
        return
    }
    try {
        await api.deleteMovement(row.id)
        ElMessage.success("O'chirildi")
        load()
    } catch (e) {
        ElMessage({ type: 'error', message: errText(e), duration: 6000, showClose: true })
    }
}

// ---------- Tahrirlash ----------
const dialog = ref(false)
const saving = ref(false)
const edit = ref(null)
const sugg = { counterparty: ref([]), region: ref([]), agent: ref([]) }

const editProducts = computed(() =>
    products.value.filter(p => p.active || p.id === edit.value?.original.productId))

async function openEdit(row) {
    edit.value = {
        id: row.id,
        type: row.movementType,
        docNo: row.docNo,
        original: row,
        movementDate: row.movementDate,
        productId: row.productId,
        fromWarehouse: row.fromWarehouse,
        toWarehouse: row.toWarehouse,
        qty: row.qty,
        price: row.price,
        counterparty: row.counterparty || '',
        region: row.region || '',
        agent: row.agent || '',
        note: row.note || '',
    }
    dialog.value = true
    try {
        const [c, r, a] = await Promise.all(
            ['counterparty', 'region', 'agent'].map(f => api.suggestions(f, groupId.value)))
        sugg.counterparty.value = c
        sugg.region.value = r
        sugg.agent.value = a
    } catch { /* avtoto'ldirishsiz ham ishlaydi */ }
}

function onTransferFrom(v) {
    edit.value.toWarehouse = v === 'nal' ? 'mediwell' : 'nal'
}

async function saveEdit() {
    const e = edit.value
    if (!e.qty || e.qty <= 0) return ElMessage.warning('Son kiritilmagan')
    saving.value = true
    try {
        await api.updateMovement(e.id, {
            movementDate: e.movementDate,
            productId: e.productId,
            fromWarehouse: e.fromWarehouse,
            toWarehouse: e.toWarehouse,
            qty: e.qty,
            price: e.type === 'sale_perech' ? e.price : null,
            counterparty: e.counterparty || null,
            region: e.region || null,
            agent: e.agent || null,
            note: e.note || null,
        })
        ElMessage.success('Saqlandi')
        dialog.value = false
        load()
    } catch (err) {
        ElMessage({ type: 'error', message: errText(err), duration: 6000, showClose: true })
    } finally {
        saving.value = false
    }
}
</script>

<template>
    <div>
        <h1 class="title">Jurnal — {{ currentGroup?.name }}</h1>
        <p class="sub">Barcha harakatlar: ko'rish, tahrirlash va o'chirish</p>

        <div class="toolbar">
            <el-date-picker v-model="filters.range" type="daterange" value-format="YYYY-MM-DD" format="DD.MM.YYYY"
                            start-placeholder="Dan" end-placeholder="Gacha" range-separator="—"
                            clearable style="width: 260px" />
            <el-select v-model="filters.type" placeholder="Tur" clearable style="width: 190px">
                <el-option v-for="(label, key) in TYPE_LABELS" :key="key" :label="label" :value="key" />
            </el-select>
            <el-select v-model="filters.productId" placeholder="Dori" filterable clearable style="width: 240px">
                <el-option v-for="p in products" :key="p.id" :label="p.name" :value="p.id" />
            </el-select>
            <el-input v-model="filters.search" placeholder="Qidiruv: kontragent, region, РМ, hujjat..." clearable
                      style="width: 300px">
                <template #prefix><el-icon><Search /></el-icon></template>
            </el-input>
        </div>

        <el-table :data="items" v-loading="loading" border stripe empty-text="Harakatlar yo'q" style="width: 100%">
            <el-table-column label="Sana" width="110" align="center">
                <template #default="{ row }">{{ fmtDate(row.movementDate) }}</template>
            </el-table-column>
            <el-table-column label="Tur" width="160" align="center">
                <template #default="{ row }">
                    <el-tag :type="TYPE_TAG[row.movementType]" size="small">{{ TYPE_LABELS[row.movementType] }}</el-tag>
                </template>
            </el-table-column>
            <el-table-column label="Dori" prop="productName" min-width="200" align="center" />
            <el-table-column label="Ombor" width="160" align="center">
                <template #default="{ row }">{{ direction(row) }}</template>
            </el-table-column>
            <el-table-column label="Son" width="100" align="center">
                <template #default="{ row }"><b class="num">{{ fmt(row.qty) }}</b></template>
            </el-table-column>
            <el-table-column label="Narx" width="120" align="center">
                <template #default="{ row }"><span class="num">{{ fmt(row.price) }}</span></template>
            </el-table-column>
            <el-table-column label="Summa" width="140" align="center">
                <template #default="{ row }"><span class="num">{{ fmt(row.amount) }}</span></template>
            </el-table-column>
            <el-table-column label="Kontragent" prop="counterparty" min-width="140" align="center" />
            <el-table-column label="Region" prop="region" width="120" align="center" />
            <el-table-column label="РМ" prop="agent" width="120" align="center" />
            <el-table-column label="Hujjat" prop="docNo" width="120" align="center" />
            <el-table-column label="Izoh" prop="note" min-width="120" align="center" show-overflow-tooltip />
            <el-table-column label="" width="100" align="center" fixed="right">
                <template #default="{ row }">
                    <el-button link type="primary" @click="openEdit(row)"><el-icon><Edit /></el-icon></el-button>
                    <el-button link type="danger" @click="remove(row)"><el-icon><Delete /></el-icon></el-button>
                </template>
            </el-table-column>
        </el-table>

        <el-pagination v-model:current-page="page" v-model:page-size="pageSize" :total="total"
                       :page-sizes="[50, 100, 200, 500]" layout="total, sizes, prev, pager, next"
                       style="margin-top: 16px; justify-content: flex-end"
                       @current-change="load" @size-change="() => { page = 1; load() }" />

        <!-- Tahrirlash dialogi -->
        <el-dialog v-model="dialog" title="Harakatni tahrirlash" width="560px" :close-on-click-modal="false">
            <el-form v-if="edit" label-width="140px">
                <el-form-item label="Tur">
                    <el-tag :type="TYPE_TAG[edit.type]">{{ TYPE_LABELS[edit.type] }}</el-tag>
                    <span v-if="edit.docNo" class="muted" style="margin-left: 10px">{{ edit.docNo }}</span>
                </el-form-item>
                <el-form-item label="Sana">
                    <el-date-picker v-model="edit.movementDate" type="date" value-format="YYYY-MM-DD"
                                    format="DD.MM.YYYY" :clearable="false" />
                </el-form-item>
                <el-form-item label="Dori">
                    <el-select v-model="edit.productId" filterable style="width: 100%">
                        <el-option v-for="p in editProducts" :key="p.id" :label="p.name" :value="p.id" />
                    </el-select>
                </el-form-item>

                <el-form-item v-if="edit.type === 'opening' || edit.type === 'receipt'" label="Qaysi omborga">
                    <el-radio-group v-model="edit.toWarehouse">
                        <el-radio-button v-for="w in WAREHOUSES" :key="w.value" :value="w.value">{{ w.label }}</el-radio-button>
                    </el-radio-group>
                </el-form-item>
                <el-form-item v-if="edit.type === 'write_off'" label="Qaysi ombordan">
                    <el-radio-group v-model="edit.fromWarehouse">
                        <el-radio-button v-for="w in WAREHOUSES" :key="w.value" :value="w.value">{{ w.label }}</el-radio-button>
                    </el-radio-group>
                </el-form-item>
                <el-form-item v-if="edit.type === 'transfer'" label="Yo'nalish">
                    <el-radio-group v-model="edit.fromWarehouse" @change="onTransferFrom">
                        <el-radio-button value="mediwell">mediwell → нал</el-radio-button>
                        <el-radio-button value="nal">нал → mediwell</el-radio-button>
                    </el-radio-group>
                </el-form-item>

                <el-form-item label="Son">
                    <el-input-number v-model="edit.qty" :min="1" :step="1" step-strictly :controls="false" />
                </el-form-item>
                <el-form-item v-if="edit.type === 'sale_perech'" label="Narx">
                    <el-input-number v-model="edit.price" :min="0" :controls="false" placeholder="Bo'sh — Переч narxi" />
                </el-form-item>
                <el-form-item v-if="edit.type === 'sale_perech'" label="Kontragent">
                    <el-autocomplete v-model="edit.counterparty" :fetch-suggestions="suggestFrom(sugg.counterparty)"
                                     clearable style="width: 100%" />
                </el-form-item>
                <el-form-item v-if="edit.type === 'sale_perech' || edit.type === 'sale_nal'" label="Region">
                    <el-autocomplete v-model="edit.region" :fetch-suggestions="suggestFrom(sugg.region)"
                                     clearable style="width: 100%" />
                </el-form-item>
                <el-form-item v-if="edit.type === 'sale_nal'" label="РМ (agent)">
                    <el-autocomplete v-model="edit.agent" :fetch-suggestions="suggestFrom(sugg.agent)"
                                     clearable style="width: 100%" />
                </el-form-item>
                <el-form-item label="Izoh">
                    <el-input v-model="edit.note" />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="dialog = false">Bekor qilish</el-button>
                <el-button type="primary" :loading="saving" @click="saveEdit">Saqlash</el-button>
            </template>
        </el-dialog>
    </div>
</template>
