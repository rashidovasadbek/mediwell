<script setup>
// Bitta hujjat formasi: kirim / ko'chirish / spisanie / переч / нал.
// Sarlavha (sana, ombor, kontragent...) + qatorlar (dori, son, [narx]).
import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { api, fmt, errText, today, suggestFrom, WAREHOUSES } from '../../sklad/skladApi'

const props = defineProps({
    type: { type: String, required: true },   // receipt | transfer | write_off | sale_perech | sale_nal
    groupId: { type: Number, required: true },
    products: { type: Array, required: true },  // aktiv dorilar
    balances: { type: Object, required: true }, // productId -> { nal, mediwell }
    suggestions: { type: Object, required: true }, // { counterparty: Ref, region: Ref, agent: Ref }
})
const emit = defineEmits(['saved'])

const isPerech = computed(() => props.type === 'sale_perech')
const isNal = computed(() => props.type === 'sale_nal')

const head = ref({
    movementDate: today(),
    toWarehouse: 'mediwell',
    fromWarehouse: props.type === 'transfer' ? 'mediwell' : 'nal',
    counterparty: '',
    region: '',
    agent: '',
    note: '',
})

let seq = 0
const newLine = () => ({ key: ++seq, productId: null, qty: null, price: null })
const lines = ref([newLine()])
const saving = ref(false)

const transferTo = computed(() => (head.value.fromWarehouse === 'nal' ? 'mediwell' : 'nal'))

const productMap = computed(() => Object.fromEntries(props.products.map(p => [p.id, p])))

function onProductChange(line) {
    const p = productMap.value[line.productId]
    if (isPerech.value) line.price = p?.perechPrice ?? null
}

const lineSum = (l) => (l.qty && l.price != null ? l.qty * l.price : null)
const totalQty = computed(() => lines.value.reduce((s, l) => s + (l.qty || 0), 0))
const totalSum = computed(() => lines.value.reduce((s, l) => s + (lineSum(l) || 0), 0))

function addLine() {
    lines.value.push(newLine())
}
function removeLine(i) {
    lines.value.splice(i, 1)
    if (lines.value.length === 0) lines.value.push(newLine())
}

function bal(productId) {
    return props.balances[productId] || { nal: 0, mediwell: 0 }
}

async function save() {
    const filled = lines.value.filter(l => l.productId)
    if (!head.value.movementDate) return ElMessage.warning('Sana tanlanmagan')
    if (filled.length === 0) return ElMessage.warning('Kamida bitta dori tanlang')
    const bad = filled.findIndex(l => !l.qty || l.qty <= 0)
    if (bad >= 0) return ElMessage.warning(`${bad + 1}-qator: son kiritilmagan`)

    const h = head.value
    const body = {
        movementDate: h.movementDate,
        groupId: props.groupId,
        movementType: props.type,
        note: h.note || null,
        lines: filled.map(l => ({
            productId: l.productId,
            qty: l.qty,
            price: isPerech.value ? l.price : null,
        })),
    }
    if (props.type === 'receipt') body.toWarehouse = h.toWarehouse
    if (props.type === 'write_off') body.fromWarehouse = h.fromWarehouse
    if (props.type === 'transfer') {
        body.fromWarehouse = h.fromWarehouse
        body.toWarehouse = transferTo.value
    }
    if (isPerech.value) {
        body.counterparty = h.counterparty || null
        body.region = h.region || null
    }
    if (isNal.value) {
        body.region = h.region || null
        body.agent = h.agent || null
    }

    saving.value = true
    try {
        const res = await api.createBatch(body)
        ElMessage.success(`Saqlandi: ${res.docNo} (${res.count} qator)`)
        // Sana va omborlar qoladi — tez ketma-ket kiritish uchun
        lines.value = [newLine()]
        h.counterparty = ''
        h.region = ''
        h.agent = ''
        h.note = ''
        emit('saved')
    } catch (e) {
        ElMessage({ type: 'error', message: errText(e), duration: 6000, showClose: true })
    } finally {
        saving.value = false
    }
}
</script>

<template>
    <div class="hujjat">
        <el-form label-position="top" class="head">
            <el-form-item label="Sana">
                <el-date-picker v-model="head.movementDate" type="date" value-format="YYYY-MM-DD"
                                format="DD.MM.YYYY" :clearable="false" style="width: 160px" />
            </el-form-item>

            <el-form-item v-if="type === 'receipt'" label="Qaysi omborga">
                <el-radio-group v-model="head.toWarehouse">
                    <el-radio-button v-for="w in WAREHOUSES" :key="w.value" :value="w.value">{{ w.label }}</el-radio-button>
                </el-radio-group>
            </el-form-item>

            <el-form-item v-if="type === 'write_off'" label="Qaysi ombordan">
                <el-radio-group v-model="head.fromWarehouse">
                    <el-radio-button v-for="w in WAREHOUSES" :key="w.value" :value="w.value">{{ w.label }}</el-radio-button>
                </el-radio-group>
            </el-form-item>

            <el-form-item v-if="type === 'transfer'" label="Qayerdan → qayerga">
                <el-radio-group v-model="head.fromWarehouse">
                    <el-radio-button value="mediwell">mediwell → нал</el-radio-button>
                    <el-radio-button value="nal">нал → mediwell</el-radio-button>
                </el-radio-group>
            </el-form-item>

            <el-form-item v-if="isPerech" label="Kontragent">
                <el-autocomplete v-model="head.counterparty" :fetch-suggestions="suggestFrom(suggestions.counterparty)"
                                 placeholder="Kontragent" clearable style="width: 240px" />
            </el-form-item>

            <el-form-item v-if="isPerech || isNal" label="Region">
                <el-autocomplete v-model="head.region" :fetch-suggestions="suggestFrom(suggestions.region)"
                                 placeholder="Region" clearable style="width: 200px" />
            </el-form-item>

            <el-form-item v-if="isNal" label="РМ (agent)">
                <el-autocomplete v-model="head.agent" :fetch-suggestions="suggestFrom(suggestions.agent)"
                                 placeholder="РМ" clearable style="width: 200px" />
            </el-form-item>

            <el-form-item label="Izoh">
                <el-input v-model="head.note" placeholder="Ixtiyoriy" style="width: 220px" />
            </el-form-item>
        </el-form>

        <el-table :data="lines" border row-key="key" style="width: 100%">
            <el-table-column label="№" type="index" width="50" align="center" />
            <el-table-column label="Dori" min-width="220" align="center">
                <template #default="{ row }">
                    <el-select v-model="row.productId" filterable placeholder="Dori tanlang" style="width: 100%"
                               @change="onProductChange(row)">
                        <el-option v-for="p in products" :key="p.id" :label="p.name" :value="p.id">
                            <span>{{ p.name }}</span>
                            <span class="muted" style="float: right; margin-left: 16px">
                                нал {{ fmt(bal(p.id).nal) }} · mediwell {{ fmt(bal(p.id).mediwell) }}
                            </span>
                        </el-option>
                    </el-select>
                </template>
            </el-table-column>
            <el-table-column label="Hozirgi qoldiq" width="150" align="center">
                <template #default="{ row }">
                    <div v-if="row.productId" class="num bal">
                        <div>нал <b>{{ fmt(bal(row.productId).nal) }}</b></div>
                        <div>mediwell <b>{{ fmt(bal(row.productId).mediwell) }}</b></div>
                    </div>
                </template>
            </el-table-column>
            <el-table-column label="Son" width="120" align="center">
                <template #default="{ row }">
                    <el-input-number v-model="row.qty" :min="1" :step="1" step-strictly :controls="false"
                                     placeholder="0" style="width: 100px" />
                </template>
            </el-table-column>
            <template v-if="isPerech">
                <el-table-column label="Narx" width="140" align="center">
                    <template #default="{ row }">
                        <el-input-number v-model="row.price" :min="0" :controls="false" placeholder="Narx"
                                         style="width: 115px" />
                    </template>
                </el-table-column>
                <el-table-column label="Summa" width="130" align="center">
                    <template #default="{ row }"><span class="num">{{ fmt(lineSum(row)) }}</span></template>
                </el-table-column>
            </template>
            <el-table-column width="56" align="center">
                <template #default="{ $index }">
                    <el-button type="danger" link @click="removeLine($index)">
                        <el-icon><Delete /></el-icon>
                    </el-button>
                </template>
            </el-table-column>
        </el-table>

        <div class="foot">
            <el-button @click="addLine"><el-icon style="margin-right: 4px"><Plus /></el-icon> qator</el-button>
            <div class="totals">
                Jami son: <b class="num">{{ fmt(totalQty) }}</b>
                <template v-if="isPerech">&nbsp;&nbsp;|&nbsp;&nbsp;Jami summa: <b class="num">{{ fmt(totalSum) }}</b></template>
            </div>
            <el-button type="primary" size="large" :loading="saving" @click="save">Saqlash</el-button>
        </div>
    </div>
</template>

<style scoped>
.head { display: flex; flex-wrap: wrap; gap: 0 20px; margin-bottom: 8px; }
.foot { display: flex; align-items: center; gap: 16px; margin-top: 14px; }
.bal { font-size: 12px; line-height: 1.4; }
.totals { flex: 1; text-align: right; color: #1a2e30; }
</style>
