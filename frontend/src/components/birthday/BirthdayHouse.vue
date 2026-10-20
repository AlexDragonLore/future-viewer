<script setup lang="ts">
withDefaults(defineProps<{ stage?: number; numeral?: string; arcana?: string }>(), {
  stage: 5, numeral: 'XXVI', arcana: 'Дом твоей мечты',
})
</script>

<template>
  <div class="house-card">
    <div class="card-corner top-left">✦</div><div class="card-corner bottom-right">✦</div>
    <span class="card-number">{{ numeral }}</span>
    <svg viewBox="0 0 400 430" fill="none" aria-hidden="true">
      <defs>
        <pattern id="birthday-grid" width="24" height="24" patternUnits="userSpaceOnUse">
          <path d="M24 0H0V24" stroke="currentColor" stroke-opacity=".07" />
        </pattern>
        <linearGradient id="birthday-window" x1="0" y1="0" x2="1" y2="1">
          <stop stop-color="#e9c77e" stop-opacity=".5" /><stop offset="1" stop-color="#e9c77e" stop-opacity=".05" />
        </linearGradient>
      </defs>
      <rect width="400" height="430" fill="url(#birthday-grid)" />
      <g stroke="currentColor" stroke-width="1">
        <path d="M32 310V360H365M52 382H352M70 374V390M330 374V390" opacity=".25" />
        <path d="M32 100H368M65 65V365M338 65V365" stroke-dasharray="3 7" opacity=".2" />
        <circle cx="200" cy="192" r="144" opacity=".12" />
        <circle cx="200" cy="192" r="133" stroke-dasharray="2 8" opacity=".2" />
      </g>
      <g class="house-layer" :class="{ built: stage >= 1 }" stroke="currentColor" stroke-width="1.8">
        <path d="M57 325L208 362L345 288L195 255Z" fill="#f0e1cb" />
        <path d="M57 325V338L208 376L345 301V288M208 362V376" />
        <path d="M70 332L208 367L332 299" stroke-width=".6" />
      </g>
      <g class="house-layer" :class="{ built: stage >= 2 }" stroke="currentColor" stroke-width="1.6">
        <path d="M80 320V208L193 154L320 189V299L208 356Z" fill="#f9f1e4" />
        <path d="M80 208L208 244L320 189M208 244V356M193 154V264" />
        <path d="M143 337V268L180 279V346" />
        <path d="M69 201L193 143L334 182V192L208 256L69 217Z" fill="#ecdbc0" />
        <path d="M69 201L208 240L334 182M208 240V256" />
        <path d="M108 192V136L193 95L288 120V177L208 217Z" fill="#fbf5eb" />
        <path d="M108 136L208 165L288 120M208 165V217" />
        <path d="M98 130L193 83L299 112V123L208 171L98 142Z" fill="#e9d4b4" />
        <path d="M98 130L208 161L299 112M208 161V171" />
      </g>
      <g class="house-layer windows" :class="{ built: stage >= 3 }" stroke="currentColor" stroke-width="1.4" fill="url(#birthday-window)">
        <path d="M222 268L304 227V292L222 335ZM249 254V321M276 241V307" />
        <path d="M119 154L193 176V203L119 183ZM143 161V190M169 168V198" />
        <path d="M221 174L275 147V176L221 203ZM248 161V189" />
        <path d="M94 243L128 253V291L94 282ZM111 248V287" />
      </g>
      <g class="house-layer" :class="{ built: stage >= 4 }" stroke="#65785e" stroke-width="1.6">
        <path d="M45 319V245M45 284C18 283 19 258 45 270C67 247 77 271 45 284M45 265C21 251 30 227 45 246C61 228 69 251 45 265" />
        <path d="M349 287V211M349 250C320 247 325 224 349 237C374 219 384 245 349 250M349 232C329 214 337 196 349 213C366 194 377 220 349 232" />
        <path d="M83 308L110 316L107 335L87 330ZM95 312V292M95 304Q76 291 95 294Q111 281 98 299" fill="#dce1cf" />
        <path d="M232 216L252 206V222L232 232ZM241 211V192M241 202Q224 191 239 193Q255 175 247 197" fill="#dce1cf" />
      </g>
      <g class="house-layer" :class="{ built: stage >= 5 }" stroke="#b88640" stroke-width="1.4">
        <circle cx="289" cy="59" r="19" fill="#ecd391" fill-opacity=".45" />
        <path d="M289 28V20M289 90V98M258 59H250M320 59H328M267 37L261 31M311 81L317 87M267 81L261 87M311 37L317 31" />
        <path d="M112 62V78M104 70H120M347 147V161M340 154H354M47 169V181M41 175H53" />
        <path d="M159 302C148 292 151 284 159 289C169 284 171 295 159 302Z" fill="#bc7d63" stroke="none" />
      </g>
    </svg>
    <div class="card-caption"><span>АРХИТЕКТУРА СЧАСТЬЯ</span><strong>{{ arcana }}</strong><i>Для того, что ещё впереди</i></div>
  </div>
</template>

<style scoped>
.house-card { position: relative; width: min(100%, 370px); padding: 28px 18px 30px; background: #faf3e7; border: 1px solid #b5986c; border-radius: 160px 160px 8px 8px; color: #8c6b46; box-shadow: 12px 19px 0 -3px #e9dfcf, 12px 19px 0 -2px #cebea4, 0 24px 60px #684f2710; }
.house-card::before { content: ''; position: absolute; inset: 9px; border: 1px solid #b5986c55; border-radius: 150px 150px 3px 3px; pointer-events: none; }
.card-number { display: block; text-align: center; font: 17px Georgia, serif; letter-spacing: .2em; margin-top: 14px; }
svg { display: block; width: 100%; margin: -7px 0 -14px; }
.house-layer { opacity: .13; transition: opacity 1s ease; }
.house-layer.built { opacity: 1; }
.card-caption { display: grid; text-align: center; gap: 9px; position: relative; }
.card-caption span { font: 9px system-ui, sans-serif; letter-spacing: .22em; }
.card-caption strong { font: 27px Georgia, serif; color: #56462f; }
.card-caption i { font: italic 13px Georgia, serif; opacity: .8; }
.card-corner { position: absolute; font-size: 14px; }
.top-left { left: 24px; bottom: 15px; }.bottom-right { right: 24px; bottom: 15px; }
@media (prefers-reduced-motion: reduce) { .house-layer { transition: none; } }
</style>
