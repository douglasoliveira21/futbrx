import { matchAudioCues, type AudioCue } from "./matchAudioCues";
import { stadiumAudioClip } from "./stadiumAudioClips";
import { stadiumMoment, type StadiumEvent } from "./stadiumDynamics";
const tracks = ["crowd", "goal", "near-miss", "kick", "whistle"] as const;
type Track = (typeof tracks)[number];
// Real CC0 field recordings: /assets/audio/credits.json. Synthesis is only an offline fallback.
export class StadiumAudio {
  private static prepared: StadiumAudio | null = null;
  static prepare() {
    if (
      typeof AudioContext === "undefined" ||
      this.prepared ||
      localStorage.getItem("bm-match-sound") === "off"
    )
      return;
    try {
      const player = new StadiumAudio(0);
      this.prepared = player;
      void player.setActive(true).catch(() => {
        void player.close();
        if (this.prepared === player) this.prepared = null;
      });
    } catch {}
  }
  static takePrepared() {
    const player = this.prepared;
    this.prepared = null;
    return player;
  }
  static releasePrepared() {
    void this.prepared?.close();
    this.prepared = null;
  }
  readonly context = new AudioContext();
  readonly ready: Promise<void>;
  loaded = 0;
  private master = this.context.createGain();
  private analyser = this.context.createAnalyser();
  private crowd = this.context.createGain();
  private effects = this.context.createGain();
  private compressor = this.context.createDynamicsCompressor();
  private reverb = this.context.createConvolver();
  private wet = this.context.createGain();
  private fallback = this.context.createGain();
  private sources = new Set<AudioBufferSourceNode>();
  private buffers = new Map<Track, AudioBuffer>();
  private second: number;
  private dribbleBeat = -1;
  private closed = false;
  private volume = 0.4;
  constructor(minute: number) {
    this.second = minute * 60 - 0.0001;
    this.master.gain.value = this.volume;
    this.compressor.threshold.value = -14;
    this.compressor.knee.value = 18;
    this.compressor.ratio.value = 4;
    this.compressor.attack.value = 0.005;
    this.compressor.release.value = 0.3;
    this.crowd.gain.value = 0.25;
    this.effects.gain.value = 0.8;
    this.wet.gain.value = 0.1;
    this.fallback.gain.value = 0.06;
    this.crowd.connect(this.compressor);
    this.effects.connect(this.compressor);
    this.effects.connect(this.reverb);
    this.reverb.connect(this.wet).connect(this.compressor);
    this.analyser.fftSize = 256;
    this.compressor
      .connect(this.master)
      .connect(this.analyser)
      .connect(this.context.destination);
    this.fallback.connect(this.crowd);
    const impulse = this.context.createBuffer(
      2,
      this.context.sampleRate * 1.4,
      this.context.sampleRate,
    );
    for (let c = 0; c < 2; c++) {
      const d = impulse.getChannelData(c);
      for (let i = 0; i < d.length; i++)
        d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / d.length, 3) * 0.22;
    }
    this.reverb.buffer = impulse;
    const noise = this.context.createBuffer(
        1,
        this.context.sampleRate * 4,
        this.context.sampleRate,
      ),
      data = noise.getChannelData(0);
    let last = 0;
    for (let i = 0; i < data.length; i++) {
      last = (last + (Math.random() * 2 - 1) * 0.05) / 1.05;
      data[i] = last;
    }
    const src = this.context.createBufferSource();
    src.buffer = noise;
    src.loop = true;
    const filter = this.context.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.value = 1100;
    src.connect(filter).connect(this.fallback);
    src.start();
    this.sources.add(src);
    this.ready = Promise.all(
      tracks.map(async (name) => {
        try {
          const response = await fetch(`/assets/audio/${name}.mp3`);
          if (!response.ok) return;
          const buffer = await this.context.decodeAudioData(
            await response.arrayBuffer(),
          );
          if (this.closed) return;
          this.buffers.set(name, buffer);
          this.loaded++;
          if (name === "crowd") this.startCrowd(buffer);
        } catch {}
      }),
    ).then(() => undefined);
  }
  private startCrowd(buffer: AudioBuffer) {
    for (const [i, x, z] of [
      [0, -48, -44],
      [1, 48, -44],
      [2, -48, 44],
      [3, 48, 44],
    ]) {
      const source = this.context.createBufferSource();
      source.buffer = buffer;
      source.loop = true;
      source.loopStart = 0.25;
      source.loopEnd = Math.max(0.5, buffer.duration - 0.3);
      source.playbackRate.value = 0.98 + i * 0.014;
      const gain = this.context.createGain();
      gain.gain.value = 0.23;
      const panner = this.panner(x, z, 70);
      source.connect(gain).connect(panner).connect(this.crowd);
      source.start(0, (i * 7) % Math.max(1, buffer.duration - 1));
      this.sources.add(source);
    }
    this.fallback.gain.setTargetAtTime(0.006, this.context.currentTime, 0.3);
  }
  level() {
    if (this.closed) return 0;
    const data = new Uint8Array(this.analyser.fftSize);
    this.analyser.getByteTimeDomainData(data);
    return Math.sqrt(
      data.reduce((sum, n) => sum + Math.pow((n - 128) / 128, 2), 0) /
        data.length,
    );
  }
  setVolume(value: number) {
    this.volume = Math.max(0, Math.min(1, value));
    this.master.gain.setTargetAtTime(
      this.volume,
      this.context.currentTime,
      0.06,
    );
  }
  listener(
    x: number,
    y: number,
    z: number,
    dx: number,
    dy: number,
    dz: number,
    ux: number,
    uy: number,
    uz: number,
  ) {
    const l = this.context.listener;
    l.positionX.value = x;
    l.positionY.value = y;
    l.positionZ.value = z;
    l.forwardX.value = dx;
    l.forwardY.value = dy;
    l.forwardZ.value = dz;
    l.upX.value = ux;
    l.upY.value = uy;
    l.upZ.value = uz;
  }
  seek(minute: number) {
    this.second = minute * 60 - 0.0001;
    this.dribbleBeat = -1;
  }
  sample(
    second: number,
    ball: { x: number; y: number; z: number },
    events: StadiumEvent[],
    players: {
      id: number;
      action: string;
      actionSecond?: number;
      x: number;
      z: number;
    }[],
    rate: number,
    homeId: string,
  ) {
    if (this.closed) return;
    const mood = stadiumMoment(second, ball, events, rate, homeId);
    this.crowd.gain.setTargetAtTime(
      0.19 + mood.energy * 0.45,
      this.context.currentTime,
      mood.reaction === "goal" ? 0.12 : 0.4,
    );
    for (const cue of matchAudioCues(this.second, second, events, players))
      this.cue(cue);
    const beat = Math.floor((second / Math.max(1, rate)) * 1.8);
    if (
      beat !== this.dribbleBeat &&
      ball.y < 0.5 &&
      players.some((p) => p.action === "dribble")
    ) {
      this.dribbleBeat = beat;
      this.play("kick", 0.11, ball.x, ball.z, 0.96, 0.22);
    }
    this.second = second;
  }
  events(minute: number, events: StadiumEvent[]) {
    for (const cue of matchAudioCues(this.second, minute * 60, events, []))
      this.cue(cue);
    this.second = minute * 60;
  }
  preview(type: "whistle" | "kick" | "goal" | "near-miss") {
    this.cue({ type });
  }
  private cue(cue: AudioCue) {
    if (cue.type === "kick" || cue.type === "header")
      this.play(
        "kick",
        cue.type === "header" ? 0.35 : 0.75,
        cue.x ?? 0,
        cue.z ?? 0,
        cue.type === "header" ? 0.75 : 1,
        0.6,
      );
    else if (cue.type === "goal") this.play("goal", 0.65, 0, -35, 1, 9);
    else if (cue.type === "near-miss")
      this.play("near-miss", 0.6, 0, -35, 1, 3.5);
    else if (cue.type === "save")
      this.play("near-miss", 0.35, 0, -35, 1.1, 1.8);
    else if (cue.type === "halftime") {
      this.whistle(0, 0.2);
      this.whistle(0.3, 0.4);
    } else if (cue.type === "fulltime") {
      this.whistle(0, 0.18);
      this.whistle(0.27, 0.18);
      this.whistle(0.57, 0.5);
    } else this.whistle(0, 0.4);
  }
  private whistle(delay: number, duration: number) {
    if (!this.play("whistle", 0.55, 0, 0, 1, duration, delay))
      this.syntheticWhistle(delay, duration);
  }
  private panner(x: number, z: number, reference = 40) {
    const p = this.context.createPanner();
    p.panningModel = "HRTF";
    p.distanceModel = "inverse";
    p.refDistance = reference;
    p.maxDistance = 250;
    p.positionX.value = x;
    p.positionY.value = 1.2;
    p.positionZ.value = z;
    return p;
  }
  private play(
    name: Track,
    volume: number,
    x: number,
    z: number,
    pitch = 1,
    length = 10,
    delay = 0,
  ) {
    const buffer = this.buffers.get(name);
    if (!buffer || this.closed) return false;
    const { offset, duration } = stadiumAudioClip(name, buffer.duration, pitch, length);
    if (duration <= 0) return false;
    const source = this.context.createBufferSource(),
      gain = this.context.createGain(),
      panner = this.panner(x, z, name === "kick" ? 35 : 80);
    const now = this.context.currentTime + delay;
    source.buffer = buffer;
    source.playbackRate.value = pitch;
    // A short fade prevents a click at the edited waveform boundary.
    gain.gain.setValueAtTime(0, now);
    gain.gain.linearRampToValueAtTime(volume, now + Math.min(0.005, duration / 4));
    gain.gain.setValueAtTime(volume, now + Math.max(0.01, duration - 0.25));
    gain.gain.linearRampToValueAtTime(0.0001, now + duration);
    source.connect(gain).connect(panner).connect(this.effects);
    source.start(now, offset);
    source.stop(now + duration);
    this.sources.add(source);
    source.onended = () => {
      source.disconnect();
      gain.disconnect();
      panner.disconnect();
      this.sources.delete(source);
    };
    return true;
  }
  private syntheticWhistle(delay: number, duration: number) {
    const osc = this.context.createOscillator(),
      gain = this.context.createGain(),
      now = this.context.currentTime + delay;
    osc.type = "sine";
    osc.frequency.setValueAtTime(3100, now);
    osc.frequency.linearRampToValueAtTime(2850, now + duration);
    gain.gain.setValueAtTime(0, now);
    gain.gain.linearRampToValueAtTime(0.08, now + 0.015);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    osc.connect(gain).connect(this.effects);
    osc.start(now);
    osc.stop(now + duration);
    osc.onended = () => {
      osc.disconnect();
      gain.disconnect();
    };
  }
  async setActive(active: boolean) {
    if (this.closed) return;
    if (active) await this.context.resume();
    else await this.context.suspend();
  }
  async close() {
    if (this.closed) return;
    this.closed = true;
    for (const source of this.sources) {
      try {
        source.stop();
        source.disconnect();
      } catch {}
    }
    this.sources.clear();
    this.buffers.clear();
    await this.context.close();
  }
}
