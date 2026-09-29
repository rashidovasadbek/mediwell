<script setup>
import { useRouter } from 'vue-router'
import { currentUser, logout } from '../auth'

const router = useRouter()

function onLogout() {
    logout()
    router.replace('/login')
}
</script>

<template>
    <div class="shell admin">
        <aside class="sidebar">
            <div class="logo">
                MEDIWELL
                <small>Admin panel</small>
            </div>

            <nav class="menu">
                <router-link to="/admin/kirim">➕ Kirim qilish</router-link>
                <router-link to="/admin/qoldiq">📊 Qoldiq</router-link>
                <router-link to="/admin/preparatlar">💊 Preparatlar</router-link>
                <router-link to="/kodlar">🏷️ Markirovka kodlar</router-link>
                <router-link to="/admin/mediwell-prixod">📥 Mediwellga prixod</router-link>
            </nav>

            <div class="user-box">
                <div class="user-name">{{ currentUser?.fullName }}</div>
                <div class="user-role">Admin</div>
                <button class="logout" @click="onLogout">Chiqish</button>
            </div>
        </aside>

        <main class="content">
            <router-view />
        </main>
    </div>
</template>

<style>
@import '../layouts/shell.css';

.shell.admin {
    --side: #01565e;
    --accent: #02c39a;
}
</style>
