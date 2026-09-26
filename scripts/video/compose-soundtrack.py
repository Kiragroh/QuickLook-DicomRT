"""Original, sample-free upbeat electronic track. 120 BPM; cuts fall on bars.

Four-on-the-floor drums, syncopated synth bass, bright chord stabs and a melodic
hook. No downloaded music, third-party recordings or imitated artist.
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

    def pluck(midi, length, bass=False):
        t = np.arange(round(rate*length), dtype=np.float32)/rate
        freq = 440*2**((midi-69)/12)
        signal = np.zeros(len(t), dtype=np.float32)
        for harmonic in range(1, 9 if bass else 12):
            signal += np.sin(2*np.pi*freq*harmonic*t) / harmonic**1.35 * np.exp(-t*harmonic*1.3)
        envelope = np.minimum(t/.006, 1)*np.exp(-t*(4 if bass else 3))*np.minimum((length-t)/.035, 1)
        return np.tanh(signal*1.4)*np.maximum(envelope,0)

    # Dmaj9, Bm7, Gmaj9, Aadd9. Each harmony lasts four bars / eight seconds.
    chords = [(50, [62, 66, 69, 73, 76]), (47, [59, 62, 66, 69, 74]),
              (43, [59, 62, 66, 69, 74]), (45, [61, 64, 69, 71, 76])]
    for start in np.arange(0, duration, 8):
        bass, pitches = chords[int(start/8) % 4]
        for i, pitch in enumerate(pitches[:4]):
            pad = note(pitch, 9, pad=True)
            pulse = .30+.70*np.minimum((np.arange(len(pad))/rate % .5)/.16,1)
            add(pad*pulse, start, .035, -.55 + i*.36)
        for beat in range(16):
            moment = start + beat*.5
            if moment >= duration-1:
                break
            # Audible forward groove from the first bar, fuller after the reveal.
            energy = .72 if moment < 8 else 1.0
            t = np.arange(int(.32*rate))/rate
            kick = np.sin(2*np.pi*(48*t+78*.035*(1-np.exp(-t/.035))))
            click = rng.normal(0,1,len(t))*np.exp(-t*330)*.13
            add((kick*np.exp(-t*14)+click)*np.minimum(t/.0015,1),moment,.33*energy)
            if beat % 2:
                t = np.arange(int(.18*rate))/rate
                noise = rng.normal(0,1,len(t))
                high = noise-np.convolve(noise,np.ones(17)/17,mode='same')
                env = np.exp(-t*30)+.7*np.exp(-np.maximum(t-.012,0)*45)*(t>=.012)+.5*np.exp(-np.maximum(t-.025,0)*40)*(t>=.025)
                add(high*env+np.sin(2*np.pi*185*t)*np.exp(-t*32)*.35,moment,.043*energy,-.08)
            for off, gain in [(0,.008),(.25,.018)]:
                t = np.arange(int(.09*rate))/rate
                noise = rng.normal(0,1,len(t))
                add(np.diff(noise,prepend=0)*np.exp(-t*(55 if off else 90)),moment+off,gain*energy,.25 if off else -.25)
            # Offbeat bass and percussive chord stabs create bounce, not a pad bed.
            add(pluck(bass-12,.42,True),moment+.25,.23*energy)
            if beat % 4 in [0,2,3]:
                for i,pitch in enumerate(pitches[:3]):
                    add(pluck(pitch,.42),moment+.25,.055*energy,-.4+i*.4)
            if moment>=8 and beat % 2==0:
                pitch = pitches[[2,4,2,1,0,2,3,1][beat//2]]+12
                hook=pluck(pitch,.6)
                add(hook,moment+.125,.063,-.2 if beat%4 else .2)
                add(hook,moment+.5,.014,.4 if beat%4 else -.4)
            if beat==15 and moment<duration-4:
                for subdivision in [.25,.375]:
                    t=np.arange(int(.05*rate))/rate
                    add(rng.normal(0,1,len(t))*np.exp(-t*80),moment+subdivision,.018,-.2)

    song = song[:round(duration*rate)]
    fade = np.minimum(np.arange(len(song))/rate/.08, 1)
    fade *= np.minimum((len(song)-np.arange(len(song)))/rate/1.5, 1)
    song *= fade[:, None]
    peak = float(np.max(np.abs(song)))
    if peak > 0:
        song *= .86 / peak
    destination = Path(destination)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(destination), 'wb') as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes((song*32767).astype('<i2').tobytes())
    metadata = dict(title='One Key - Upbeat', bpm=120, beatsPerBar=4, barSeconds=2,
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
