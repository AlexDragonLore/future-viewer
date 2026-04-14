let ctx: AudioContext | null = null

function getCtx(): AudioContext {
  if (!ctx) ctx = new AudioContext()
  if (ctx.state === 'suspended') ctx.resume()
  return ctx
}

/** Мистический тон при переворачивании карты */
export function playCardFlip() {
  try {
    const ac = getCtx()
    const now = ac.currentTime

    const osc = ac.createOscillator()
    const gain = ac.createGain()
    osc.connect(gain)
    gain.connect(ac.destination)

    osc.type = 'sine'
    osc.frequency.setValueAtTime(660, now)
    osc.frequency.exponentialRampToValueAtTime(330, now + 0.18)

    gain.gain.setValueAtTime(0.22, now)
    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.35)

    osc.start(now)
    osc.stop(now + 0.35)
  } catch {
    // AudioContext может быть заблокирован до первого жеста
  }
}

/** Шум перемешивания карт */
export function playShuffle() {
  try {
    const ac = getCtx()
    const now = ac.currentTime
    const duration = 0.6

    const bufferSize = Math.floor(ac.sampleRate * duration)
    const buffer = ac.createBuffer(1, bufferSize, ac.sampleRate)
    const data = buffer.getChannelData(0)
    for (let i = 0; i < bufferSize; i++) {
      data[i] = Math.random() * 2 - 1
    }

    const source = ac.createBufferSource()
    source.buffer = buffer

    const filter = ac.createBiquadFilter()
    filter.type = 'bandpass'
    filter.frequency.value = 3000
    filter.Q.value = 0.8

    const gain = ac.createGain()
    gain.gain.setValueAtTime(0.0, now)
    gain.gain.linearRampToValueAtTime(0.18, now + 0.08)
    gain.gain.setValueAtTime(0.18, now + 0.35)
    gain.gain.exponentialRampToValueAtTime(0.001, now + duration)

    source.connect(filter)
    filter.connect(gain)
    gain.connect(ac.destination)

    source.start(now)
    source.stop(now + duration)
  } catch {
    //
  }
}

/** Мягкий звук при раздаче карты */
export function playDeal() {
  try {
    const ac = getCtx()
    const now = ac.currentTime
    const duration = 0.15

    const bufferSize = Math.floor(ac.sampleRate * duration)
    const buffer = ac.createBuffer(1, bufferSize, ac.sampleRate)
    const data = buffer.getChannelData(0)
    for (let i = 0; i < bufferSize; i++) {
      data[i] = Math.random() * 2 - 1
    }

    const source = ac.createBufferSource()
    source.buffer = buffer

    const filter = ac.createBiquadFilter()
    filter.type = 'highpass'
    filter.frequency.value = 4000

    const gain = ac.createGain()
    gain.gain.setValueAtTime(0.12, now)
    gain.gain.exponentialRampToValueAtTime(0.001, now + duration)

    source.connect(filter)
    filter.connect(gain)
    gain.connect(ac.destination)

    source.start(now)
    source.stop(now + duration)
  } catch {
    //
  }
}

/** Мистический аккорд финального раскрытия */
export function playReveal() {
  try {
    const ac = getCtx()
    const now = ac.currentTime
    // Минорный аккорд Am: A2 C3 E3
    const freqs = [110, 130.81, 164.81]

    freqs.forEach((freq, i) => {
      const osc = ac.createOscillator()
      const gain = ac.createGain()

      osc.type = 'sine'
      osc.frequency.value = freq

      const t = now + i * 0.07
      gain.gain.setValueAtTime(0, t)
      gain.gain.linearRampToValueAtTime(0.09, t + 0.12)
      gain.gain.setValueAtTime(0.09, t + 0.5)
      gain.gain.exponentialRampToValueAtTime(0.001, t + 1.8)

      osc.connect(gain)
      gain.connect(ac.destination)
      osc.start(t)
      osc.stop(t + 1.8)
    })
  } catch {
    //
  }
}
