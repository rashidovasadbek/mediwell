<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { currentUser, logout } from '../auth'
import ChangePasswordDialog from '../components/ChangePasswordDialog.vue'

const router = useRouter()
const pwDialog = ref(false)

function onLogout() {
    logout()
    router.replace('/login')
}
</script>

<template>
    <div class="shell sklad">
        <aside class="sidebar">
            <div class="logo">
                MEDIWELL
                <small>Sklad paneli</small>
            </div>

            <nav class="menu">
                <router-link to="/sklad/chiqim">📤 Chiqim qilish</router-link>
            </nav>

            <div class="user-box">
                <div class="user-name">{{ currentUser?.fullName }}</div>
                <div class="user-role">Skladchi</div>
                <button class="logout" style="margin-bottom: 8px" @click="pwDialog = true">Parolni almashtirish</button>
                <button class="logout" @click="onLogout">Chiqish</button>
            </div>
            <ChangePasswordDialog v-model="pwDialog" />
        </aside>

        <main class="content">
            <router-view />
        </main>
    </div>
</template>

<style>
@import '../layouts/shell.css';

.shell.sklad {
    --side: #1b3a5c;
    --accent: #4aa3df;
}
</style>
