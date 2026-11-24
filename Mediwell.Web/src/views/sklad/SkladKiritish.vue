<script setup>
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { groupId, currentGroup } from '../../sklad/useGroup'
import { api, errText } from '../../sklad/skladApi'
import HujjatForm from './HujjatForm.vue'

const tab = ref('sale_perech')
const products = ref([])
const balances = ref({})
const suggestions = {
    counterparty: ref([]),
    region: ref([]),
    agent: ref([]),
}

const tabs = [
    { name: 'receipt', label: 'Kirim' },
    { name: 'transfer', label: "Ko'chirish" },
    { name: 'write_off', label: 'Spisanie' },
    { name: 'sale_perech', label: 'Переч sotuv' },
    { name: 'sale_nal', label: 'Нал sotuv' },
]

async function loadBalances() {
    const rows = await api.balance(groupId.value)
    balances.value = Object.fromEntries(rows.map(r => [r.productId, { nal: r.nal, mediwell: r.mediwell }]))
}

async function loadSuggestions() {
    const [c, r, a] = await Promise.all(
        ['counterparty', 'region', 'agent'].map(f => api.suggestions(f, groupId.value)))
    suggestions.counterparty.value = c
    suggestions.region.value = r
    suggestions.agent.value = a
}

async function load() {
    if (!groupId.value) return
    try {
        products.value = await api.products(groupId.value)
        await Promise.all([loadBalances(), loadSuggestions()])
    } catch (e) {
        ElMessage.error(errText(e))
    }
}

async function onSaved() {
    try {
        await Promise.all([loadBalances(), loadSuggestions()])
    } catch (e) {
        ElMessage.error(errText(e))
    }
}

watch(groupId, load, { immediate: true })
</script>

<template>
    <div>
        <h1 class="title">Harakat qo'shish — {{ currentGroup?.name }}</h1>
        <p class="sub">Kirim, ko'chirish, spisanie va sotuvlarni hujjat ko'rinishida kiritish</p>

        <el-alert v-if="products.length === 0" type="warning" :closable="false" show-icon style="margin-bottom: 16px"
                  title="Bu guruhda faol dori yo'q. Avval «Dorilar» sahifasida dori qo'shing." />

        <el-card>
            <el-tabs v-model="tab">
                <!-- Guruh o'zgarsa forma qayta yaratiladi (qatorlar tozalanadi) -->
                <el-tab-pane v-for="t in tabs" :key="t.name" :name="t.name" :label="t.label">
                    <HujjatForm :key="`${t.name}-${groupId}`" :type="t.name" :group-id="groupId"
                                :products="products" :balances="balances" :suggestions="suggestions"
                                @saved="onSaved" />
                </el-tab-pane>
            </el-tabs>
        </el-card>
    </div>
</template>
