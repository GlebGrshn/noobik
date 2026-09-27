"""Synthesises the prototype sound effects into Assets/Game/Resources/Audio.

Deterministic, dependency-free (standard library only). Re-run after changing a recipe:
    py -3 Tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Game', 'Resources', 'Audio')


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def add(target, source, at=0.0, gain=1.0):
    start = int(at * RATE)
    if len(target) < start + len(source):
        target.extend([0.0] * (start + len(source) - len(target)))
    for i, value in enumerate(source):
        target[start + i] += value * gain
    return target


def tone(freq, seconds, decay, harmonics=((1, 1.0),), attack=0.004, sweep=0.0):
    out = []
    phase = 0.0
    for i in range(int(RATE * seconds)):
        t = i / RATE
        f = freq * (1 + sweep * t / seconds)
        phase += 2 * math.pi * f / RATE
        env = min(1.0, t / attack) * math.exp(-t / decay)
        out.append(env * sum(g * math.sin(phase * h) for h, g in harmonics))
    return out


def noise(seconds, decay, cutoff, rng, attack=0.002, highpass=False):
    out = []
    low = 0.0
    alpha = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    for i in range(int(RATE * seconds)):
        t = i / RATE
        white = rng.uniform(-1, 1)
        low += alpha * (white - low)
        value = white - low if highpass else low
        out.append(value * min(1.0, t / attack) * math.exp(-t / decay))
    return out


def fade(samples, seconds=0.01):
    n = min(len(samples), int(RATE * seconds))
    for i in range(n):
        samples[-1 - i] *= i / n
    return samples


def write(name, samples, peak=0.85):
    top = max(abs(v) for v in samples) or 1.0
    frames = b''.join(struct.pack('<h', int(max(-1, min(1, v / top * peak)) * 32767)) for v in fade(samples))
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(frames)


def main():
    os.makedirs(OUT, exist_ok=True)
    rng = random.Random(28092026)

    dirt = add(noise(0.22, 0.06, 900, rng), tone(95, 0.18, 0.05, sweep=-0.4), gain=0.9)
    write('dig_dirt', dirt)

    stone = noise(0.05, 0.012, 3000, rng, highpass=True)
    add(stone, tone(1250, 0.28, 0.07, ((1, 1.0), (1.5, 0.6), (2.36, 0.4))), gain=0.7)
    add(stone, tone(120, 0.12, 0.03), gain=0.6)
    write('dig_stone', stone)

    write('coin', tone(2093, 0.1, 0.03, ((1, 1.0), (2, 0.2))), peak=0.5)

    pickup = tone(1046.5, 0.12, 0.05, ((1, 1.0), (2, 0.3)))
    add(pickup, tone(1568, 0.22, 0.08, ((1, 1.0), (2, 0.3))), at=0.07)
    write('pickup', pickup)

    chest = silence(0.05)
    for i, note in enumerate((523.3, 659.3, 784.0, 1046.5)):
        add(chest, tone(note, 0.3, 0.1, ((1, 1.0), (3, 0.15))), at=i * 0.07)
    write('chest', chest)

    fanfare = silence(0.05)
    for i, note in enumerate((523.3, 659.3, 784.0, 1046.5, 1318.5, 1568.0)):
        add(fanfare, tone(note, 0.6 if i == 5 else 0.3, 0.25 if i == 5 else 0.1, ((1, 1.0), (2, 0.25), (3, 0.1))), at=i * 0.09)
    write('collect', fanfare)

    sell = silence(0.05)
    for i in range(7):
        add(sell, tone(rng.uniform(1900, 2600), 0.09, 0.025), at=i * 0.045 + rng.uniform(0, 0.015), gain=0.8)
    write('sell', sell)

    buy = noise(0.08, 0.03, 4000, rng, highpass=True)
    add(buy, tone(1318.5, 0.18, 0.07, ((1, 1.0), (2, 0.3))), at=0.05)
    add(buy, tone(1760, 0.32, 0.12, ((1, 1.0), (2, 0.3))), at=0.13)
    write('buy', buy)

    swell = silence(0.01)
    for freq in (110, 164.8, 220):
        add(swell, tone(freq, 1.7, 0.7, ((1, 1.0), (2, 0.2)), attack=0.45), gain=0.5)
    write('zone', swell)

    rumble = noise(2.4, 0.9, 140, rng, attack=0.3)
    add(rumble, tone(55, 2.4, 0.9, attack=0.3), gain=0.8)
    write('door', rumble)


if __name__ == '__main__':
    main()
