// Web Audio 벨소리. docs/prototype.html의 오디오 코드를 옮긴 것이다.
// 음원(voice)마다 bus(gain) -> muffle(lowpass) -> panner(HRTF) -> master 체인을 둔다.
// Unity는 왼손 좌표계라서 위치와 방향의 z를 반전해 Web Audio(오른손)에 넘긴다.
// 디버그: 브라우저 콘솔에서 window.__missedCallAudio() 로 상태를 볼 수 있다.
var MissedCallAudioLib = {
  $MCA: {
    ctx: null,
    master: null,
    masterLevel: 0.85,
    paused: false,
    voices: [],
    rolloff: 2.2,
    timer: 0,
    lookahead: 0.14,
    waves: ['sine', 'square', 'triangle', 'sawtooth'],
    listener: [0, 0, 0, 0, 0, 1],

    setPosition: function (node, x, y, z) {
      if (node.positionX) {
        node.positionX.value = x;
        node.positionY.value = y;
        node.positionZ.value = z;
      } else {
        node.setPosition(x, y, z);
      }
    },

    playNote: function (v, freq, at) {
      var ctx = MCA.ctx;
      var amp = ctx.createGain();
      var osc = ctx.createOscillator();
      var peak = Math.max(v.level, 0.0002);
      osc.type = MCA.waves[v.wave] || 'square';
      osc.frequency.value = freq;
      amp.gain.setValueAtTime(0.0001, at);
      amp.gain.exponentialRampToValueAtTime(peak, at + 0.008);
      amp.gain.exponentialRampToValueAtTime(0.0001, at + 0.16);
      osc.connect(amp);
      osc.start(at);
      osc.stop(at + 0.18);
      if (v.shimmer) {
        var shimmer = ctx.createOscillator();
        shimmer.type = 'sine';
        shimmer.frequency.value = freq * 2.01;
        shimmer.connect(amp);
        shimmer.start(at);
        shimmer.stop(at + 0.18);
      }
      amp.connect(v.bus);
    },

    playBuzz: function (v, at) {
      var ctx = MCA.ctx;
      var osc = ctx.createOscillator();
      var amp = ctx.createGain();
      osc.type = 'sawtooth';
      osc.frequency.value = 158;
      amp.gain.setValueAtTime(0.0001, at);
      amp.gain.linearRampToValueAtTime(0.45, at + 0.02);
      amp.gain.setValueAtTime(0.45, at + 0.3);
      amp.gain.linearRampToValueAtTime(0.0001, at + 0.34);
      osc.connect(amp);
      amp.connect(v.bus);
      osc.start(at);
      osc.stop(at + 0.36);
    },

    // 박자 예약은 Unity 프레임이 아니라 이 타이머가 맡는다. 프레임이 끊겨도 박자가 흔들리지 않는다.
    tick: function () {
      var ctx = MCA.ctx;
      if (!ctx || ctx.state !== 'running' || MCA.paused) return;
      var now = ctx.currentTime;
      for (var i = 0; i < MCA.voices.length; i++) {
        var v = MCA.voices[i];
        if (!v || !v.running) continue;
        if (v.next < now) {
          // 탭이 멈췄다 돌아온 경우 밀린 음을 한꺼번에 울리지 않고 처음부터 다시 시작한다.
          v.next = now + 0.05;
          v.index = 0;
        }
        while (v.next < now + MCA.lookahead) {
          if (v.index === 0 && v.buzz) MCA.playBuzz(v, v.next);
          MCA.playNote(v, v.notes[v.index], v.next);
          v.index = (v.index + 1) % v.notes.length;
          v.next += v.gap + (v.index === 0 ? v.pause : 0);
        }
      }
    },

    status: function () {
      var ctx = MCA.ctx;
      var list = [];
      for (var i = 0; i < MCA.voices.length; i++) {
        var v = MCA.voices[i];
        list.push({
          id: v.id,
          running: v.running,
          cutoff: Math.round(v.muffle.frequency.value),
          gain: Math.round(v.bus.gain.value * 1000) / 1000,
          position: [v.x, v.y, v.z]
        });
      }
      return {
        state: ctx ? ctx.state : 'none',
        sampleRate: ctx ? ctx.sampleRate : 0,
        listener: MCA.listener.slice(),
        voices: list
      };
    }
  },

  MC_Audio_Init: function (rolloff) {
    if (MCA.ctx) return 1;
    var Ctx = window.AudioContext || window.webkitAudioContext;
    if (!Ctx) return 0;
    MCA.ctx = new Ctx();
    MCA.rolloff = rolloff;
    MCA.master = MCA.ctx.createGain();
    MCA.master.gain.value = MCA.paused ? 0 : MCA.masterLevel;
    MCA.master.connect(MCA.ctx.destination);

    // 브라우저는 사용자 입력 이벤트 안에서만 오디오를 시작시킨다.
    // Unity 입력 콜백은 이벤트 호출 스택 밖에서 돌기 때문에 여기서 직접 듣는다.
    var resume = function () {
      if (MCA.ctx && MCA.ctx.state !== 'running') MCA.ctx.resume();
    };
    var events = ['pointerdown', 'pointerup', 'mousedown', 'touchend', 'keydown'];
    for (var i = 0; i < events.length; i++) window.addEventListener(events[i], resume, true);
    resume();

    MCA.timer = setInterval(MCA.tick, 25);
    window.__missedCallAudio = MCA.status;
    return 1;
  },

  MC_Audio_State: function () {
    if (!MCA.ctx) return 0;
    return MCA.ctx.state === 'running' ? 2 : 1;
  },

  MC_Audio_CreateVoice: function (notesPtr, count, noteGap, pause, wave, shimmer, buzz, level) {
    var ctx = MCA.ctx;
    if (!ctx) return -1;
    var notes = [];
    for (var i = 0; i < count; i++) notes.push(HEAPF32[(notesPtr >> 2) + i]);

    var bus = ctx.createGain();
    bus.gain.value = 0;
    var muffle = ctx.createBiquadFilter();
    muffle.type = 'lowpass';
    muffle.frequency.value = 7000;
    muffle.Q.value = 0.7;
    var panner = ctx.createPanner();
    panner.panningModel = 'HRTF';
    panner.distanceModel = 'inverse';
    panner.refDistance = 0.8;
    panner.rolloffFactor = MCA.rolloff;
    panner.maxDistance = 20;
    bus.connect(muffle);
    muffle.connect(panner);
    panner.connect(MCA.master);

    var id = MCA.voices.length;
    MCA.voices.push({
      id: id, notes: notes, gap: noteGap, pause: pause, wave: wave,
      shimmer: shimmer !== 0, buzz: buzz !== 0, level: level,
      bus: bus, muffle: muffle, panner: panner,
      running: false, next: 0, index: 0, gain: 0.75, x: 0, y: 0, z: 0
    });
    return id;
  },

  MC_Audio_StartVoice: function (id) {
    var v = MCA.voices[id];
    if (!v) return;
    var now = MCA.ctx.currentTime;
    v.running = true;
    v.index = 0;
    v.next = now + 0.05;
    v.bus.gain.cancelScheduledValues(now);
    v.bus.gain.setValueAtTime(v.gain, now);
  },

  MC_Audio_StopVoice: function (id) {
    var v = MCA.voices[id];
    if (!v) return;
    v.running = false;
    v.bus.gain.setTargetAtTime(0, MCA.ctx.currentTime, 0.012);
  },

  MC_Audio_SetVoice: function (id, x, y, z, cutoff, gain) {
    var v = MCA.voices[id];
    if (!v) return;
    var now = MCA.ctx.currentTime;
    v.x = x; v.y = y; v.z = z;
    MCA.setPosition(v.panner, x, y, -z);
    v.muffle.frequency.setTargetAtTime(cutoff, now, 0.05);
    v.gain = gain;
    if (v.running) v.bus.gain.setTargetAtTime(gain, now, 0.1);
  },

  MC_Audio_SetListener: function (px, py, pz, fx, fy, fz) {
    if (!MCA.ctx) return;
    MCA.listener = [px, py, pz, fx, fy, fz];
    var l = MCA.ctx.listener;
    if (l.positionX) {
      l.positionX.value = px; l.positionY.value = py; l.positionZ.value = -pz;
      l.forwardX.value = fx; l.forwardY.value = fy; l.forwardZ.value = -fz;
      l.upX.value = 0; l.upY.value = 1; l.upZ.value = 0;
    } else {
      l.setPosition(px, py, -pz);
      l.setOrientation(fx, fy, -fz, 0, 1, 0);
    }
  },

  MC_Audio_OneShot: function (wave, frequency, length, level, delay) {
    var ctx = MCA.ctx;
    if (!ctx || ctx.state !== 'running') return;
    var t = ctx.currentTime + delay;
    var osc = ctx.createOscillator();
    var amp = ctx.createGain();
    osc.type = MCA.waves[wave] || 'sine';
    osc.frequency.setValueAtTime(frequency, t);
    osc.frequency.exponentialRampToValueAtTime(Math.max(40, frequency * 0.5), t + length);
    amp.gain.setValueAtTime(Math.max(level, 0.0002), t);
    amp.gain.exponentialRampToValueAtTime(0.0001, t + length);
    osc.connect(amp);
    amp.connect(MCA.master);
    osc.start(t);
    osc.stop(t + length + 0.02);
  },

  // 일시정지. AudioContext.suspend()는 쓰지 않는다: 위의 resume 리스너가 메뉴 클릭에서 다시 켜 버린다.
  // 대신 박자 예약을 멈추고 마스터 볼륨을 내린다. 이미 예약된 음(lookahead 안)은 볼륨으로 가린다.
  MC_Audio_SetPaused: function (paused) {
    MCA.paused = !!paused;
    var ctx = MCA.ctx;
    if (!ctx) return;
    var now = ctx.currentTime;
    MCA.master.gain.cancelScheduledValues(now);
    MCA.master.gain.setTargetAtTime(MCA.paused ? 0 : MCA.masterLevel, now, 0.015);
    if (!MCA.paused) {
      // 재개하면 벨 패턴을 처음부터 다시 울린다.
      for (var i = 0; i < MCA.voices.length; i++) {
        var v = MCA.voices[i];
        if (v && v.running) {
          v.next = now + 0.05;
          v.index = 0;
        }
      }
    }
  },

  MC_Audio_Shutdown: function () {
    if (!MCA.ctx) return;
    for (var i = 0; i < MCA.voices.length; i++) MCA.voices[i].running = false;
    clearInterval(MCA.timer);
  }
};

autoAddDeps(MissedCallAudioLib, '$MCA');
mergeInto(LibraryManager.library, MissedCallAudioLib);
