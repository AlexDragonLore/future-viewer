<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowRight, Check, Download, Gift, KeyRound, LoaderCircle, LockKeyhole, RotateCcw, Sparkles } from 'lucide-vue-next'
import { birthdayApi, type BirthdayGift, type BirthdaySession } from '@/api/birthdayApi'
import BirthdayHouse from '@/components/birthday/BirthdayHouse.vue'

const router = useRouter()
const route = useRoute()
const STORAGE_KEY = 'fv_birthday_ticket_v1'
const session = ref<BirthdaySession | null>(null)
const key = ref('')
const busy = ref(false)
const restoring = ref(true)
const error = ref('')
const feedback = ref('')
const accepted = ref(false)
const selected = ref('')
const intro = ref(false)
const gift = ref<BirthdayGift | null>(null)
const postcard = ref('')
const certificate = ref('')
const showCertificate = ref(false)
const heading = ref<HTMLElement>()
const stages = ['Основа', 'Замысел', 'Свет', 'Жизнь', 'Тепло']
const complete = computed(() => !!session.value && session.value.completedSteps === session.value.totalSteps)
const question = computed(() => session.value?.question)
const isPlaying = computed(() => session.value && !intro.value && !complete.value)
let alive = true

function save(value: BirthdaySession) {
  session.value = value
  try { sessionStorage.setItem(STORAGE_KEY, value.ticket) } catch { /* The game also works without browser storage. */ }
}
function clearSession() {
  session.value = null
  try { sessionStorage.removeItem(STORAGE_KEY) } catch { /* Storage may be disabled. */ }
}
function handleError(cause: unknown) {
  const response = (cause as { response?: { status?: number; data?: { message?: string } } }).response
  if (response?.status === 401) {
    clearSession()
    clearAssets()
  }
  error.value = response?.status === 429 ? 'Немного передохнём. Попробуй ещё раз через несколько минут.'
    : response?.data?.message || 'Не удалось связаться с сайтом. Проверь интернет и попробуй ещё раз — твои очки сохранены.'
}
async function focusHeading() {
  await nextTick()
  heading.value?.focus({ preventScroll: true })
  if (window.matchMedia('(max-width: 760px)').matches) heading.value?.scrollIntoView({ block: 'start', behavior: 'instant' })
}
async function unlock() {
  if (busy.value || !key.value.trim()) return
  busy.value = true
  error.value = ''
  try {
    const result = await birthdayApi.unlock(key.value.trim())
    if (!alive) return
    save(result)
    key.value = ''
    intro.value = true
    feedback.value = ''
    accepted.value = false
    await focusHeading()
  } catch (cause) { if (alive) handleError(cause) }
  finally { busy.value = false }
}
async function answer(optionId: string) {
  if (busy.value || accepted.value || !session.value || !question.value) return
  selected.value = optionId
  busy.value = true
  error.value = ''
  feedback.value = ''
  try {
    const result = await birthdayApi.answer(session.value.ticket, question.value.id, optionId)
    if (!alive) return
    save(result.session)
    feedback.value = result.message
    accepted.value = result.correct
    if (result.correct) await focusHeading()
  } catch (cause) { if (alive) handleError(cause) }
  finally { busy.value = false }
}
async function nextStage() {
  accepted.value = false
  feedback.value = ''
  selected.value = ''
  if (complete.value) await revealGift()
  await focusHeading()
}
function clearAssets() {
  if (postcard.value) URL.revokeObjectURL(postcard.value)
  if (certificate.value) URL.revokeObjectURL(certificate.value)
  postcard.value = ''
  certificate.value = ''
  gift.value = null
  showCertificate.value = false
}
async function revealGift() {
  if (!session.value || busy.value) return
  busy.value = true
  error.value = ''
  const ticket = session.value.ticket
  try {
    const [details, card, voucher] = await Promise.all([
      birthdayApi.gift(ticket), birthdayApi.asset(ticket, 'postcard'), birthdayApi.asset(ticket, 'certificate'),
    ])
    if (!alive) return
    clearAssets()
    gift.value = details
    postcard.value = URL.createObjectURL(card)
    certificate.value = URL.createObjectURL(voucher)
  } catch (cause) { if (alive) handleError(cause) }
  finally { busy.value = false }
}
async function openInvitation(hash: string) {
  // Fragments are never sent in HTTP requests. Remove the key from the URL and
  // router state immediately, before exchanging it for an encrypted game ticket.
  const linkKey = new URLSearchParams(hash.slice(1)).get('key')
  if (hash) await router.replace({ path: '/birthday', hash: '', query: {} })
  if (linkKey) {
    clearAssets()
    clearSession()
    key.value = linkKey
    await unlock()
  }
  return !!linkKey
}
watch(() => route.hash, async hash => {
  if (!hash || restoring.value || busy.value) return
  restoring.value = true
  await openInvitation(hash)
  restoring.value = false
})
onMounted(async () => {
  if (!await openInvitation(window.location.hash)) {
    let ticket: string | null = null
    try { ticket = sessionStorage.getItem(STORAGE_KEY) } catch { /* Optional persistence. */ }
    if (ticket) {
      try {
        const result = await birthdayApi.resume(ticket)
        if (alive) { save(result); if (complete.value) await revealGift() }
      } catch (cause) { if (alive) handleError(cause) }
    }
  }
  restoring.value = false
})
onBeforeUnmount(() => { alive = false; clearAssets() })
</script>

<template>
  <main class="birthday-page ym-disable-keys" data-testid="birthday-page">
    <header class="birthday-header">
      <a href="/" class="birthday-brand"><Sparkles :size="19" /> Вуаль Грядущего <span>/</span> <em>особенная история</em></a>
      <span class="private-label"><LockKeyhole :size="13" /> Только по приглашению</span>
    </header>

    <div v-if="restoring" class="birthday-loading" role="status"><LoaderCircle class="spin" :size="28" /><p>Открываем твоё приглашение…</p></div>

    <template v-else>
      <section v-if="!session" class="birthday-welcome">
        <div class="welcome-copy">
          <p class="eyebrow"><span></span> МАЛЕНЬКАЯ ТАЙНА · БОЛЬШОЙ ПОВОД</p>
          <h1 ref="heading" tabindex="-1">У каждого дома<br>есть своя <em>магия.</em></h1>
          <p class="lead">А у этой истории — особенная гостья.<br>Здесь архитектура встречается с таро,<br class="desktop-break"> а в конце тебя ждёт кое-что настоящее.</p>
          <form class="key-form" @submit.prevent="unlock">
            <label for="birthday-key">Твой секретный ключ</label>
            <div class="key-input"><KeyRound :size="18" /><input id="birthday-key" v-model="key" type="password" autocomplete="off" maxlength="256" placeholder="Ключ из приглашения" :disabled="busy" aria-describedby="key-note"></div>
            <button class="primary-button" :disabled="busy || !key.trim()"><LoaderCircle v-if="busy" class="spin" :size="17" /><span>{{ busy ? 'Открываем…' : 'Открыть приглашение' }}</span><ArrowRight :size="17" /></button>
            <p id="key-note" class="tiny">Есть секретная ссылка? Просто открой её — ключ подставится сам.</p>
          </form>
        </div>
        <div class="welcome-art"><span class="orbit-note">a little magic, just for you</span><BirthdayHouse /><span class="art-note">Пять арканов. Один особенный подарок.</span></div>
      </section>

      <section v-else-if="intro" class="birthday-welcome">
        <div class="welcome-copy">
          <p class="eyebrow"><span></span> ЛИЧНОЕ ПРИГЛАШЕНИЕ ДЛЯ ДАШИ</p>
          <h1 ref="heading" tabindex="-1">Спроектируем<br>немного <em>счастья?</em></h1>
          <p class="lead">Даша, с днём рождения! Сегодня карты предлагают тебе особенный проект — дом твоей мечты.</p>
          <div class="intro-rules">
            <p><span>01</span> Открой пять арканов и реши их архитектурные загадки.</p>
            <p><span>02</span> Собирай по 100 очков. Подсказки помогут, если задумалась.</p>
            <p><span>03</span> Набери 500 очков — и забери свой подарок.</p>
          </div>
          <button class="primary-button" @click="intro = false; focusHeading()">Начать маленькое приключение <ArrowRight :size="18" /></button>
          <p class="tiny">Около 5 минут · Без таймера · С любовью к деталям</p>
        </div>
        <div class="welcome-art"><span class="orbit-note">chapter 26 · the best is yet to come</span><BirthdayHouse :stage="0" /><span class="art-note">Твой новый год. Твой новый проект.</span></div>
      </section>

      <section v-else-if="accepted || isPlaying" class="game-section">
        <div class="game-topline"><p class="eyebrow">ДОМ ТВОЕЙ МЕЧТЫ</p><span class="score" aria-live="polite"><Sparkles :size="16" /> {{ session.score }} <small>/ 500 очков</small></span></div>
        <ol class="stage-track" aria-label="Этапы проекта">
          <li v-for="(stage, i) in stages" :key="stage" :class="{ done: i < session.completedSteps, current: i === session.completedSteps }" :aria-current="i === session.completedSteps ? 'step' : undefined">
            <span><Check v-if="i < session.completedSteps" :size="14" /><template v-else>{{ String(i + 1).padStart(2, '0') }}</template></span><b>{{ stage }}</b>
          </li>
        </ol>
        <div class="game-layout">
          <aside class="game-art"><BirthdayHouse :stage="session.completedSteps" :numeral="accepted ? '✦' : question?.numeral" :arcana="accepted ? 'На шаг ближе к мечте' : question?.arcana" /><p>С каждым ответом твой дом оживает.</p></aside>
          <div class="game-content">
            <div v-if="accepted" class="success-card">
              <div class="success-seal"><Check :size="30" /></div>
              <p class="eyebrow">ЕЩЁ ОДИН АРКАН В КОЛЛЕКЦИИ</p>
              <h1 ref="heading" tabindex="-1">{{ complete ? 'Дом мечты готов!' : 'Красиво придумано.' }}</h1>
              <p class="success-points">+100 <span>очков</span></p>
              <p class="lead" role="status">{{ feedback }}</p>
              <button class="primary-button" :disabled="busy" @click="nextStage">{{ complete ? 'Открыть мой подарок' : 'К следующему аркану' }}<Gift v-if="complete" :size="18" /><ArrowRight v-else :size="18" /></button>
            </div>
            <template v-else-if="question">
              <p class="eyebrow">АРКАН {{ question.numeral }} · {{ question.arcana.toUpperCase() }}</p>
              <h1 ref="heading" tabindex="-1">{{ question.title }}</h1>
              <p class="question-text">{{ question.text }}</p>
              <div class="answer-options" :aria-busy="busy">
                <button v-for="(option, i) in question.options" :key="question.id + option.id" :disabled="busy" class="answer-option" :class="{ chosen: selected === option.id }" @click="answer(option.id)">
                  <span class="option-letter">{{ ['А', 'Б', 'В'][i] }}</span><span><strong>{{ option.title }}</strong><small>{{ option.description }}</small></span><LoaderCircle v-if="busy && selected === option.id" class="spin option-arrow" :size="18" /><ArrowRight v-else class="option-arrow" :size="17" />
                </button>
              </div>
              <p v-if="feedback" class="hint" role="status"><Sparkles :size="19" /><span><strong>Маленькая подсказка</strong>{{ feedback }} Попробуй ещё раз.</span></p>
              <p v-else class="tiny">Выбери один ответ. Здесь можно ошибаться и пробовать снова.</p>
            </template>
          </div>
        </div>
      </section>

      <section v-else-if="complete" class="gift-section">
        <template v-if="gift">
          <div class="gift-heading"><p class="eyebrow">ПРОЕКТ ЗАВЕРШЁН · ВСЕ ПЯТЬ АРКАНОВ СОБРАНЫ</p><h1 ref="heading" tabindex="-1">Даша, это всё <em>для тебя.</em></h1><p>500 очков, дом мечты и ещё один повод улыбнуться.</p></div>
          <div class="gift-layout">
            <figure class="postcard"><img :src="postcard" alt="Даша, с 26-летием! Пусть вдохновение, красота и смелые идеи превращаются в любимые проекты." width="1024" height="1280"><figcaption>Пусть самые красивые проекты будут впереди ♡</figcaption></figure>
            <div class="gift-copy">
              <span class="gift-badge"><Gift :size="16" /> ТВОЙ ЗАСЛУЖЕННЫЙ ПОДАРОК</span>
              <h2>За 500 очков —<br><strong>{{ gift.amount.toLocaleString('ru-RU') }} ₽</strong><br><span>на то, что тебя порадует.</span></h2>
              <p class="gift-provider">Подарочный сертификат Ozon</p>
              <p class="gift-message">На новую идею, маленькую мечту или просто что-то прекрасное. Пусть в твоих 26 будет много света, смелых замыслов и людей, с которыми тепло.</p>
              <p class="signature">С днём рождения, Даша! <span>♡</span></p>
              <a class="primary-button" :href="certificate" download="Даше — сертификат Ozon 15000.jpg"><Download :size="18" /> Скачать сертификат</a>
              <button class="text-button" :aria-expanded="showCertificate" @click="showCertificate = !showCertificate">{{ showCertificate ? 'Скрыть сертификат' : 'Посмотреть сертификат и код' }}<ArrowRight :size="16" /></button>
              <div class="gift-note"><LockKeyhole :size="16" /><p>Код и инструкция — внутри сертификата. Скачивание не активирует подарок, поэтому эту страницу можно спокойно открыть ещё раз.</p></div>
              <a class="postcard-download" :href="postcard" download="С днём рождения, Даша.jpg">Сохранить открытку</a>
            </div>
          </div>
          <div v-if="showCertificate" class="certificate-panel"><img :src="certificate" alt="Подарочный сертификат Ozon на 15 000 рублей, код и инструкция по активации" width="591" height="1280"></div>
          <p class="replay-note"><RotateCcw :size="14" /> Хочешь пройти ещё раз? Открой исходную секретную ссылку — подарок останется доступен.</p>
        </template>
        <div v-else class="gift-pending"><Gift :size="40" /><h1 ref="heading" tabindex="-1">Твой подарок уже ждёт.</h1><p>Все 500 очков на месте.</p><button class="primary-button" :disabled="busy" @click="revealGift"><LoaderCircle v-if="busy" class="spin" :size="18" />{{ busy ? 'Открываем подарок…' : 'Открыть подарок' }}</button></div>
      </section>
      <div v-if="error" class="birthday-error" role="alert">{{ error }}</div>
    </template>
    <footer class="birthday-footer"><span>СОЗДАНО ДЛЯ ОДНОГО ОСОБЕННОГО ДНЯ</span><span>архитектура × таро × немного любви</span><Sparkles :size="16" /></footer>
  </main>
</template>

<style scoped>
.birthday-page { --ink: #483d32; --muted: #7b7162; --accent: #a1654c; --line: #dfd4c2; min-height: 100svh; color-scheme: light; background: radial-gradient(ellipse at 88% 33%, #ede2cd66, transparent 45%), #faf6ef; color: var(--ink); font-family: system-ui, -apple-system, sans-serif; padding: 0 clamp(20px, 5.5vw, 88px); display: flex; flex-direction: column; }
.birthday-header { min-height: 89px; border-bottom: 1px solid var(--line); display: flex; align-items: center; justify-content: space-between; gap: 20px; }
.birthday-brand { display: flex; align-items: center; gap: 12px; color: var(--ink); text-decoration: none; font: 17px Georgia, serif; }.birthday-brand svg { color: #a77b41; }.birthday-brand span { color: #c8b9a4; }.birthday-brand em { font-size: 14px; color: var(--muted); }
.private-label { display: flex; align-items: center; gap: 7px; font-size: 11px; color: var(--muted); }
.birthday-welcome { max-width: 1150px; width: 100%; margin: auto; padding: 75px 0 83px; display: grid; grid-template-columns: 1.15fr 1fr; gap: 60px; align-items: center; }
.eyebrow { color: #8b765c; font-size: 10px; font-weight: 600; letter-spacing: .18em; display: flex; align-items: center; gap: 10px; line-height: 1.7; margin: 0 0 24px; }.eyebrow>span { width: 23px; height: 1px; background: #ae8a56; }
h1,h2 { font-weight: 400; letter-spacing: -.035em; font-family: Georgia, 'Times New Roman', serif; }
h1 { font-size: clamp(38px, 4.5vw, 61px); line-height: 1.12; margin: 0 0 26px; outline: none; scroll-margin-top: 22px; }
h1 em { color: var(--accent); font-weight: 400; }
.lead { font-size: 16px; line-height: 1.9; color: var(--muted); max-width: 475px; margin: 0 0 28px; }
.welcome-art { display: flex; align-items: center; flex-direction: column; gap: 32px; padding: 0 18px; }.orbit-note { color: #96795a; font: italic 15px Georgia, serif; transform: rotate(-5deg); }.art-note { color: #8c7c65; font-size: 11px; margin-top: 8px; }
.key-form { max-width: 370px; margin-top: 30px; }.key-form label { font-size: 11px; display: block; margin-bottom: 10px; color: #77684f; }.key-input { display: flex; gap: 12px; align-items: center; padding: 14px 15px; border: 1px solid #d1c3ad; border-radius: 5px; background: #fffaf4; margin-bottom: 12px; color: #ad8c5e; }.key-input:focus-within { outline: 2px solid #a1654c; outline-offset: 2px; }.key-input input { border: 0; outline: 0; width: 100%; background: transparent; font-size: 14px; color: var(--ink); }.key-input input::placeholder { color: #9b907f; }
.primary-button { display: inline-flex; align-items: center; justify-content: center; gap: 14px; min-height: 51px; padding: 14px 23px; border: 1px solid #77513c; background: #77513c; color: #fff8ee; border-radius: 5px; font: 500 13px/1.6 system-ui, sans-serif; text-decoration: none; cursor: pointer; transition: background .2s, transform .2s; }.primary-button:hover:not(:disabled) { background: #60412f; transform: translateY(-1px); }.primary-button:disabled { opacity: .55; cursor: wait; }.key-form .primary-button { width: 100%; }
button:focus-visible,a:focus-visible { outline: 2px solid #a1654c; outline-offset: 5px; }.tiny { color: #8a7e6e; font-size: 11px; line-height: 1.7; margin: 15px 0 0; }
.intro-rules { margin: 28px 0 30px; max-width: 440px; }.intro-rules p { display: flex; align-items: baseline; gap: 16px; font-size: 13px; line-height: 1.8; margin: 12px 0; }.intro-rules span { font: italic 17px Georgia, serif; color: #b18b60; }
.birthday-footer { display: flex; justify-content: space-between; align-items: center; border-top: 1px solid var(--line); color: #97866d; min-height: 69px; gap: 15px; margin-top: auto; }.birthday-footer span:first-child { font-size: 8px; letter-spacing: .16em; }.birthday-footer span:nth-child(2) { font: italic 12px Georgia, serif; }
.birthday-loading { min-height: 65vh; display: flex; flex-direction: column; justify-content: center; align-items: center; gap: 20px; font-size: 15px; }
.game-section { width: 100%; max-width: 1100px; margin: 0 auto; padding: 36px 0 70px; }.game-topline { display: flex; justify-content: space-between; align-items: center; gap: 14px; }.game-topline .eyebrow { margin: 0; }.score { display: flex; gap: 8px; align-items: center; font-size: 18px; color: #82603a; }.score small { font-size: 11px; color: var(--muted); }
.stage-track { display: flex; list-style: none; padding: 0 0 25px; margin: 26px 0 36px; border-bottom: 1px solid var(--line); gap: 10px; }.stage-track li { flex: 1; display: flex; align-items: center; gap: 9px; font-size: 11px; color: #9e9383; }.stage-track li>span { width: 27px; height: 27px; border: 1px solid #d5cbbd; border-radius: 50%; display: grid; place-items: center; font-size: 10px; }.stage-track b { font-weight: 400; }.stage-track .done { color: #677557; }.stage-track .done>span { background: #e8ecdf; border-color: #b5bea5; }.stage-track .current { color: #795335; }.stage-track .current>span { background: #795335; color: #fff; border-color: #795335; }
.game-layout { display: grid; grid-template-columns: .8fr 1.2fr; gap: 80px; align-items: center; }.game-art { display: grid; justify-items: center; padding: 0 15px 0 0; }.game-art :deep(.house-card) { max-width: 300px; }.game-art>p { margin: 34px 0 0; color: #938169; font: italic 13px Georgia, serif; }.game-content h1 { font-size: clamp(30px, 3.2vw, 43px); line-height: 1.2; margin-bottom: 20px; }.game-content .eyebrow { margin-bottom: 16px; }.question-text { color: var(--muted); font-size: 14px; line-height: 1.8; margin: 0 0 25px; }
.answer-options { display: grid; gap: 11px; }.answer-option { display: flex; align-items: center; gap: 14px; width: 100%; background: #fffcf6; border: 1px solid #dfd4c2; border-radius: 6px; color: var(--ink); padding: 17px 15px; text-align: left; cursor: pointer; transition: border-color .2s, background .2s; }.answer-option:hover:not(:disabled),.answer-option.chosen { background: #f2eadd; border-color: #a98b60; }.answer-option:disabled { cursor: wait; opacity: .7; }.option-letter { font: 15px Georgia, serif; color: #a3855c; border: 1px solid #e4d9c7; border-radius: 50%; flex: 0 0 28px; height: 28px; display: grid; place-items: center; }.answer-option strong { display: block; font-size: 13px; font-weight: 550; margin-bottom: 5px; }.answer-option small { display: block; font-size: 11px; line-height: 1.7; color: #867969; }.option-arrow { margin-left: auto; flex-shrink: 0; color: #a38a69; }
.hint { display: flex; gap: 12px; padding: 15px 17px; background: #f1e6d4; border-radius: 5px; font-size: 12px; line-height: 1.7; margin: 18px 0 0; }.hint svg { flex-shrink: 0; margin-top: 2px; }.hint strong { display: block; font-weight: 600; margin-bottom: 3px; }
.success-card { padding: 25px 0; }.success-seal { display: grid; place-items: center; width: 64px; height: 64px; border-radius: 50%; border: 1px solid #b8c0a5; background: #e8ecdf; color: #768061; margin-bottom: 27px; }.success-points { color: #a1654c; font: 48px Georgia, serif; margin: 20px 0; }.success-points span { font-size: 20px; }.success-card .lead { font-size: 15px; }
.gift-section { max-width: 1050px; width: 100%; margin: 0 auto; padding: 45px 0 50px; }.gift-heading { text-align: center; margin-bottom: 40px; }.gift-heading .eyebrow { justify-content: center; margin-bottom: 15px; }.gift-heading h1 { margin-bottom: 15px; font-size: clamp(38px, 4.3vw, 56px); }.gift-heading>p:last-child { color: var(--muted); font-size: 13px; }.gift-layout { display: grid; grid-template-columns: 1fr 1fr; gap: 65px; align-items: center; }.postcard { margin: 0; padding: 10px 10px 16px; background: #fff; box-shadow: 0 16px 55px #5b3d2114; border: 1px solid #e7ddce; transform: rotate(-2deg); }.postcard img { display: block; width: 100%; height: auto; }.postcard figcaption { text-align: center; font: italic 13px/1.6 Georgia, serif; padding: 15px 4px 0; color: #8f7458; }.gift-badge { display: flex; align-items: center; gap: 9px; color: #926e44; font-size: 9px; letter-spacing: .12em; }.gift-copy h2 { font-size: 30px; line-height: 1.4; margin: 24px 0 16px; }.gift-copy h2 strong { font-size: clamp(53px, 5.5vw, 74px); font-weight: 400; color: #a1654c; letter-spacing: -.04em; }.gift-copy h2 span { font-size: 24px; }.gift-provider { font-size: 13px; color: #6f634f; margin: 0 0 22px; }.gift-message { font-size: 13px; line-height: 1.9; color: var(--muted); }.signature { font: italic 21px Georgia, serif; color: #92684c; margin: 23px 0 28px; }.signature span { margin-left: 6px; font-size: 27px; }.gift-copy .primary-button { width: 100%; }.text-button { background: none; border: 0; display: flex; align-items: center; justify-content: center; width: 100%; gap: 8px; color: #8c6348; font-size: 12px; padding: 17px 0; cursor: pointer; }.gift-note { display: flex; gap: 10px; border-top: 1px solid var(--line); padding: 17px 0 12px; color: #958671; }.gift-note svg { flex-shrink: 0; margin-top: 3px; }.gift-note p { font-size: 10px; line-height: 1.8; margin: 0; }.postcard-download { font-size: 11px; color: #87684c; text-underline-offset: 4px; }.certificate-panel { background: #ede4d6; padding: 20px; border-radius: 8px; margin: 40px 0; display: grid; justify-items: center; }.certificate-panel img { width: min(100%, 591px); height: auto; }.replay-note { display: flex; justify-content: center; align-items: center; gap: 8px; font-size: 10px; line-height: 1.8; color: #958671; margin-top: 40px; }.gift-pending { min-height: 55vh; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 20px; text-align: center; }.gift-pending h1 { font-size: 37px; margin: 0; }.birthday-error { width: min(100%, 600px); align-self: center; margin: 0 0 30px; border: 1px solid #cf9980; background: #f5e8df; color: #874a35; border-radius: 5px; padding: 16px 20px; font-size: 13px; line-height: 1.7; }
.spin { animation: birthday-spin 1s linear infinite; }@keyframes birthday-spin { to { transform: rotate(360deg); } }
@media (max-width: 1000px) { .birthday-welcome { gap: 25px; padding: 55px 0 65px; }.game-layout { gap: 40px; }.gift-layout { gap: 35px; }.welcome-art { padding: 0 8px; } }
@media (max-width: 760px) { .birthday-header { min-height: 71px; }.birthday-brand { font-size: 15px; gap: 8px; }.birthday-brand em,.birthday-brand>span,.private-label { display: none; }.birthday-welcome { grid-template-columns: 1fr; gap: 45px; padding-top: 38px; }.welcome-copy { max-width: 520px; }.welcome-copy h1 { font-size: clamp(38px, 7.7vw, 53px); }.lead { font-size: 14px; line-height: 1.8; }.eyebrow { font-size: 8px; letter-spacing: .14em; margin-bottom: 21px; }.key-form { max-width: 100%; }.welcome-art :deep(.house-card) { max-width: 290px; }.welcome-art { gap: 25px; }.intro-rules p { font-size: 12px; }.birthday-footer { flex-wrap: wrap; justify-content: center; padding: 20px 0; min-height: 80px; column-gap: 30px; row-gap: 10px; }.birthday-footer span:nth-child(2) { font-size: 11px; }.birthday-footer svg { display: none; }.game-section { padding-top: 25px; }.game-topline .eyebrow { font-size: 8px; }.score { font-size: 16px; gap: 5px; }.score small { font-size: 10px; }.stage-track { margin: 24px 0 28px; gap: 4px; padding-bottom: 20px; }.stage-track li { flex-direction: column; gap: 7px; font-size: 9px; }.game-layout { grid-template-columns: 1fr; gap: 32px; }.game-art { order: 2; padding: 0; }.game-art :deep(.house-card) { max-width: 240px; }.game-content h1 { font-size: 33px; }.question-text { font-size: 13px; }.answer-option { padding: 15px 12px; gap: 11px; }.answer-option strong { font-size: 12px; }.answer-option small { font-size: 11px; }.option-arrow { width: 14px; }.success-card { padding-top: 0; }.success-card .primary-button { width: 100%; }.gift-section { padding-top: 32px; }.gift-heading { margin-bottom: 30px; }.gift-heading h1 { font-size: 39px; }.gift-heading .eyebrow { font-size: 7px; }.gift-heading>p:last-child { font-size: 12px; line-height: 1.7; }.gift-layout { grid-template-columns: 1fr; gap: 38px; }.postcard { max-width: 430px; justify-self: center; transform: rotate(-1deg); }.gift-copy { max-width: 470px; justify-self: center; }.gift-copy h2 { margin-top: 20px; }.gift-copy h2 strong { font-size: 68px; }.gift-copy h2 span { font-size: 23px; }.replay-note { align-items: flex-start; }.replay-note svg { flex-shrink: 0; margin-top: 3px; }.certificate-panel { padding: 8px; }.desktop-break { display: none; } }
@media (prefers-reduced-motion: reduce) { *,*::before,*::after { animation: none !important; transition: none !important; } }
</style>
