<script setup lang="ts">
import { ref } from 'vue'
import { storeToRefs } from 'pinia'
import { usePublicConfigStore } from '@/stores/usePublicConfigStore'

const open = ref(false)

const { supportEmail } = storeToRefs(usePublicConfigStore())
</script>

<template>
  <footer class="site-footer">
    <button class="about-link" @click="open = true">О нас</button>
    <span class="mx-2 text-mystic-silver/30">·</span>
    <span class="text-mystic-silver/40">Вуаль Грядущего</span>
    <template v-if="supportEmail">
      <span class="mx-2 text-mystic-silver/30">·</span>
      <span class="text-mystic-silver/40">
        Связаться с нами:
        <a class="support-link" :href="`mailto:${supportEmail}`">{{ supportEmail }}</a>
      </span>
    </template>
  </footer>

  <Transition name="fade">
    <div v-if="open" class="about-overlay" @click.self="open = false">
      <div class="mystic-card about-modal">
        <button class="back-btn" @click="open = false">← Назад</button>
        <div class="text-mystic-accent text-xs tracking-[0.4em] mb-3 text-center">✦ ИСТОРИЯ ✦</div>
        <h2 class="font-display text-3xl gold-text text-center mb-6">О нас</h2>

        <div class="story text-mystic-silver/85 leading-relaxed space-y-4">
          <p>
            Эта история началась с одного человека — <strong class="text-mystic-accent">Александра Дунцева</strong>,
            руководителя группы разработки.
          </p>
          <p>
            Однажды, устав от бесконечных споров на планированиях, Александр решил довериться древней мудрости Таро.
            Он начал раскладывать карты на каждую задачу — и результаты оказались ошеломляющими. Оценки
            стали точнее, сроки — предсказуемее, а команда — спокойнее.
          </p>
          <p>
            Слух о новом методе быстро разлетелся по коридорам <strong class="text-mystic-accent">Ozon</strong>.
            Команды одна за другой перенимали практику. Чтобы не привлекать лишнего внимания, систему замаскировали
            под невинным названием — <em>«покер-планирование»</em>. Снаружи выглядело как безобидная игра в карты,
            внутри — настоящий ритуал предвидения.
          </p>
          <p>
            Но Александр понял: нельзя скрывать такой дар от мира. Он решил открыть людям глаза и выложил это
            приложение в открытый доступ. Более того — расширил его возможности: теперь «Вуаль Грядущего» помогает
            оценивать не только IT-задачи, но и любые вопросы жизни, от сложных решений до простых сомнений.
          </p>
          <p>
            Наш ИИ становится точнее благодаря вам: мы анализируем ваши отклики на расклады и используем их
            как обучающий сигнал, чтобы интерпретации в будущем лучше отражали реальность. Никаких персональных
            данных — только ощущения от предсказаний.
          </p>
          <p class="text-center text-mystic-accent/80 italic pt-2">
            Доверься картам. Они знают больше, чем кажется.
          </p>
        </div>

        <div class="text-center mt-8">
          <button class="glow-button" @click="open = false">Закрыть</button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.site-footer {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 15;
  text-align: center;
  padding: 0.75rem 1rem;
  font-size: 0.8rem;
  letter-spacing: 0.1em;
  pointer-events: auto;
}
.about-link {
  color: #f5c26b;
  background: transparent;
  border: none;
  cursor: pointer;
  font-family: 'Cinzel', serif;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  transition: text-shadow 0.3s ease;
}
.about-link:hover {
  text-shadow: 0 0 12px rgba(245, 194, 107, 0.8);
}
.support-link {
  color: #f5c26b;
  text-decoration: none;
  transition: text-shadow 0.3s ease;
}
.support-link:hover {
  text-shadow: 0 0 10px rgba(245, 194, 107, 0.7);
}
.about-overlay {
  position: fixed;
  inset: 0;
  z-index: 50;
  background: rgba(6, 2, 16, 0.78);
  backdrop-filter: blur(6px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.5rem;
  overflow-y: auto;
}
.about-modal {
  position: relative;
  max-width: 38rem;
  width: 100%;
  padding: 2.5rem 2rem;
  max-height: 90vh;
  overflow-y: auto;
}
.back-btn {
  display: inline-flex;
  align-items: center;
  background: transparent;
  border: none;
  color: rgba(245, 194, 107, 0.7);
  font-family: 'Cinzel', serif;
  font-size: 0.8rem;
  letter-spacing: 0.1em;
  cursor: pointer;
  padding: 0.25rem 0.5rem;
  margin-bottom: 1rem;
  transition: color 0.2s ease, text-shadow 0.2s ease;
}
.back-btn:hover {
  color: #f5c26b;
  text-shadow: 0 0 10px rgba(245, 194, 107, 0.6);
}
.story p {
  font-size: 0.95rem;
}
@media (max-width: 480px) {
  .about-overlay {
    padding: 0.75rem;
  }
  .about-modal {
    padding: 2rem 1.25rem;
    max-height: 92vh;
  }
  .story p {
    font-size: 0.9rem;
  }
}
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.35s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
