<script setup lang="ts">
import Starfield from '@/components/fx/Starfield.vue'
import MistLayer from '@/components/fx/MistLayer.vue'
</script>

<template>
  <div class="min-h-screen relative overflow-hidden bg-gradient-to-b from-mystic-deepest via-mystic-deep to-mystic-mid text-mystic-silver font-body">
    <Starfield />
    <MistLayer />
    <div class="relative z-10">
      <RouterView v-slot="{ Component }">
        <Transition name="fade" mode="out-in">
          <component :is="Component" />
        </Transition>
      </RouterView>
    </div>
    <div class="pointer-events-none fixed inset-0 z-20" style="box-shadow: inset 0 0 200px 60px rgba(0,0,0,0.7);"></div>
  </div>
</template>

<style>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.5s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
