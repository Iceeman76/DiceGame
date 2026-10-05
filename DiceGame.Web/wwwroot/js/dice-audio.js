let context;
let master;
let muted = false;
let loading;
const buffers = new Map();
const voices = new Set();
const announcementVolume = 0.6;
const lastRoundVolume = announcementVolume * 0.6;
const clips = [
  "dice-throw-1",
  "dice-throw-2",
  "dice-throw-3",
  "dice-grab-1",
  "dice-grab-2",
  "dice-shake-1",
  "chip-lay-1",
  "die-throw-1",
];

export async function enable() {
  try {
    if (!context) {
      const AudioContext = window.AudioContext || window.webkitAudioContext;
      if (!AudioContext) return;
      context = new AudioContext();
      master = context.createGain();
      master.gain.value = muted ? 0 : 0.6;
      master.connect(context.destination);
    }
    if (context.state === "suspended") await context.resume();
    loading ??= Promise.allSettled(
      clips.map(async (clip) => {
        const response = await fetch(
          new URL(`../audio/${clip}.ogg`, import.meta.url),
        );
        if (!response.ok) return;
        const decoded = await context.decodeAudioData(
          await response.arrayBuffer(),
        );
        buffers.set(clip, decoded);
      }),
    );
    await loading;
  } catch {
    muted = true;
  }
}

export function setMuted(value) {
  muted = value;
  if (value && window.speechSynthesis) window.speechSynthesis.cancel();
  if (master && context.state !== "closed")
    master.gain.setTargetAtTime(value ? 0 : 0.6, context.currentTime, 0.02);
}

function sample(clip, offset = 0, volume = 1) {
  const buffer = buffers.get(clip);
  if (!buffer) return;
  const source = context.createBufferSource();
  source.buffer = buffer;
  source.playbackRate.value = 0.96 + Math.random() * 0.08;
  const gain = context.createGain();
  gain.gain.value = volume;
  source.connect(gain).connect(master);
  voices.add(source);
  source.start(context.currentTime + offset);
  source.onended = () => {
    voices.delete(source);
    source.disconnect();
    gain.disconnect();
  };
}

function fanfare() {
  const harmonics = context.createPeriodicWave(new Float32Array(6), new Float32Array([0, 1, 0.5, 0.25, 0.12, 0.06]));
  for (const [frequency, offset, duration] of [[523.25, 0, 0.16], [698.46, 0.22, 0.4]]) {
    const source = context.createOscillator();
    const gain = context.createGain();
    const start = context.currentTime + offset;
    source.setPeriodicWave(harmonics);
    source.frequency.value = frequency;
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.linearRampToValueAtTime(0.22, start + 0.015);
    gain.gain.exponentialRampToValueAtTime(0.14, start + 0.07);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
    source.connect(gain).connect(master);
    voices.add(source);
    source.start(start);
    source.stop(start + duration);
    source.onended = () => {
      voices.delete(source);
      source.disconnect();
      gain.disconnect();
    };
  }
}

function announce(text, volume = announcementVolume) {
  if (!window.speechSynthesis || !window.SpeechSynthesisUtterance) return;
  const announcement = new window.SpeechSynthesisUtterance(text);
  const americanVoices = window.speechSynthesis.getVoices().filter(voice => voice.lang === "en-US");
  const voice = americanVoices.find(voice => /David|Guy|Eric|Christopher|Andrew/i.test(voice.name)) ?? americanVoices[0];
  if (voice) announcement.voice = voice;
  announcement.lang = "en-US";
  announcement.volume = volume;
  window.speechSynthesis.speak(announcement);
}

export function play(sound) {
  if (sound === "last-round" && !muted) {
    announce("Last round!", lastRoundVolume);
    if (context && context.state === "running") sample("chip-lay-1", 0, lastRoundVolume);
    return;
  }
  if (!context || context.state !== "running" || muted) return;
  switch (sound) {
    case "three-ones":
      fanfare();
      break;
    case "roll":
      sample("dice-shake-1", 0, 0.35);
      sample(`dice-throw-${1 + Math.floor(Math.random() * 3)}`, 0.15, 0.8);
      break;
    case "keep":
      sample(`dice-grab-${1 + Math.floor(Math.random() * 2)}`, 0, 0.6);
      break;
    case "bank":
      sample("chip-lay-1", 0, 0.65);
      break;
    case "bust":
      sample("die-throw-1", 0, 0.3);
      break;
  }
}

export function stop() {
  if (window.speechSynthesis) window.speechSynthesis.cancel();
  for (const source of voices) source.stop();
  voices.clear();
}

export async function dispose() {
  stop();
  if (context) await context.close();
  context = undefined;
  master = undefined;
  loading = undefined;
  buffers.clear();
}
