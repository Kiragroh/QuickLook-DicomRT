"""Original, sample-free instrumental bed. 120 BPM; every film cut falls on a bar.

Deterministic additive synthesis: soft electric piano, warm pads, bass and quiet
percussion. No downloaded music, third-party recordings or imitated artist.
"""
import argparse
import json
import wave
from pathlib import Path
import numpy as np


def compose(destination, duration=112):
    rate = 48000
    song = np.zeros((int((duration + 4) * rate), 2), dtype=np.float32)
    rng = np.random.default_rng(260926)

    def add(signal, start, gain=1, pan=0):
        offset = round(start * rate)
        end = min(len(song), offset + len(signal))
        if end <= offset:
            return
        signal = signal[:end-offset] * gain
        song[offset:end, 0] += signal * np.sqrt((1-pan)/2)
        song[offset:end, 1] += signal * np.sqrt((1+pan)/2)

    def note(midi, length, pad=False):
        t = np.arange(round(rate * length), dtype=np.float32) / rate
        freq = 440 * 2**((midi-69)/12)
        if pad:
            signal = (np.sin(2*np.pi*freq*t) + .28*np.sin(2*np.pi*freq*1.002*t)
                      + .14*np.sin(2*np.pi*freq*2*t)) / 1.42
            envelope = np.minimum(t/.5, 1) * np.minimum((length-t)/1.1, 1)
        else:
            signal = (np.sin(2*np.pi*freq*t) + .3*np.sin(2*np.pi*freq*2*t)*np.exp(-t*2)
                      + .08*np.sin(2*np.pi*freq*3*t)*np.exp(-t*4))
            envelope = np.minimum(t/.012, 1) * np.exp(-t*2.0) * np.minimum((length-t)/.2, 1)
        return signal * np.maximum(envelope, 0)

    # Dmaj9, Bm7, Gmaj9, Aadd9. Each harmony lasts four bars / eight seconds.
    chords = [(50, [62, 66, 69, 73, 76]), (47, [59, 62, 66, 69, 74]),
              (43, [59, 62, 66, 69, 74]), (45, [61, 64, 69, 71, 76])]
    for start in np.arange(0, duration, 8):
        bass, pitches = chords[int(start/8) % 4]
        for i, pitch in enumerate(pitches[:4]):
            add(note(pitch, 9, pad=True), start, .035, -.55 + i*.36)
        for beat in range(16):
            moment = start + beat*.5
            if moment >= duration-2:
                break
            if beat % 2 == 0:
                pitch = pitches[[0, 2, 1, 3, 2, 4, 1, 3][beat//2]]
                tone = note(pitch+12, 2.2)
                add(tone, moment, .055, -.3 if beat % 4 else .3)
                add(tone, moment+.375, .014, .5 if beat % 4 else -.5)
            if beat % 4 == 0:
                add(note(bass-12, 1.6), moment, .11)
            if 8 <= moment < duration-4:
                if beat % 4 == 0:
                    t = np.arange(int(.24*rate))/rate
                    kick = np.sin(2*np.pi*(47*t+35*.045*(1-np.exp(-t/.045))))
                    add(kick*np.exp(-t*23)*np.minimum(t/.003,1), moment, .11)
                if beat % 4 == 2:
                    t = np.arange(int(.13*rate))/rate
                    noise = rng.normal(0,1,len(t))
                    filtered = np.diff(noise, prepend=0)
                    add(filtered*np.exp(-t*45)*np.minimum(t/.003,1), moment, .012, -.1)
                if beat % 2:
                    t = np.arange(int(.06*rate))/rate
                    noise = rng.normal(0,1,len(t))
                    add(np.diff(noise,prepend=0)*np.exp(-t*80),moment,.004,.35)

    song = song[:round(duration*rate)]
    fade = np.minimum(np.arange(len(song))/rate/2, 1)
    fade *= np.minimum((len(song)-np.arange(len(song)))/rate/3, 1)
    song *= fade[:, None]
    peak = float(np.max(np.abs(song)))
    if peak > 0:
        song *= .65 / peak
    destination = Path(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(destination), 'wb') as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes((song*32767).astype('<i2').tobytes())
    metadata = dict(title='One Key', bpm=120, beatsPerBar=4, barSeconds=2,
                    duration=duration, sampleRate=rate, channels=2,
                    source='Original deterministic synthesis; no third-party samples',
                    peak=float(np.max(np.abs(song))), rms=float(np.sqrt(np.mean(song**2))))
    destination.with_suffix('.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
    print(json.dumps(metadata))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('destination')
    parser.add_argument('--duration', type=float, default=112)
    args = parser.parse_args()
    compose(args.destination, args.duration)
