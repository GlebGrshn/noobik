"""Synthesises the prototype sound effects into Assets/Game/Resources/Audio.

Deterministic, dependency-free (standard library only). Re-run after changing a recipe:
    py -3 Tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave
import argparse

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


def write(name, samples, peak=0.85, loop=False):
    top = max(abs(v) for v in samples) or 1.0
    if not loop:
        fade(samples)
    frames = b''.join(struct.pack('<h', int(max(-1, min(1, v / top * peak)) * 32767)) for v in samples)
    with wave.open(os.path.join(OUT, name + '.wav'), 'wb') as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(frames)


def seamless(samples, overlap=0.5):
    """Overlap the tail into the head; retain matching waveform slopes at the loop seam."""
    n = int(RATE * overlap)
    for i in range(n):
        t = i / n
        samples[i] = samples[i] * t + samples[-n + i] * (1 - t)
    return samples[:-n]


def new_audio(force_ambience=False):
    # A separate seed leaves all existing effect recipes byte-for-byte unchanged.
    rng = random.Random(29092026)
    write('click', add(tone(880, .065, .018), noise(.025, .008, 1700, rng), gain=.3), .32)
    write('step', add(noise(.16, .045, 900, rng), tone(105, .13, .032), gain=.4), .6)
    write('step_stone', add(noise(.14, .035, 2700, rng), tone(240, .12, .025), gain=.45), .65)
    write('land', add(noise(.38, .08, 650, rng), tone(72, .28, .06), gain=1.2), .8)
    write('clank', add(tone(790, .7, .18, ((1, 1), (1.47, .55), (2.63, .3))), noise(.05, .012, 3800, rng), gain=.4), .8)
    write('sizzle', noise(.48, .13, 2600, rng, highpass=True), .6)
    drill = noise(.19, .2, 1300, rng)
    add(drill, tone(88, .19, .3, ((1, 1), (2, .5), (4, .3)), sweep=.15), gain=.6)
    write('drill', drill, .65)
    harpoon = add(noise(.15, .035, 2300, rng), tone(480, .34, .08, sweep=-.65), gain=.7)
    add(harpoon, tone(85, .2, .04), gain=.6)
    write('harpoon', harpoon, .85)
    write('boss_hit', add(noise(.24, .06, 500, rng), tone(130, .27, .065, sweep=-.4), gain=1.2), .75)
    roar = noise(2.3, .85, 480, rng, attack=.18)
    add(roar, tone(48, 2.3, .9, ((1, 1), (1.51, .4), (3, .2)), attack=.18, sweep=-.3), gain=.8)
    for i in range(len(roar)):
        roar[i] *= .7 + .3 * math.sin(i / RATE * 2 * math.pi * 13)
    write('roar', roar, .85)
    slam = add(noise(1.35, .3, 300, rng), tone(52, 1.2, .3, sweep=-.35), gain=1.5)
    add(slam, noise(.12, .03, 1700, rng), gain=.5)
    write('slam', slam, .9)
    warning = tone(196, .7, .24, ((1, 1), (1.06, .45), (2, .1)), attack=.025)
    write('warn', warning, .55)
    lava = noise(8.5, 1e9, 260, rng, attack=.001)
    for i in range(24):
        add(lava, tone(rng.uniform(65, 150), .25, .055, sweep=-.7), at=rng.uniform(0, 8), gain=.12)
    write('lava', seamless(lava), .45, True)
    hum = silence(8.5)
    for f, g in ((73, .6), (110, .3), (147, .16), (294, .035)):
        add(hum, tone(f, 8.5, 1e9, attack=.001), gain=g)
    write('hum', seamless(hum), .4, True)

    # Temporary ambience, not music: distinct beds and sparse environmental events.
    # Do not overwrite a supplied replacement (including a different extension).
    places = ('surface', 'house', 'roots', 'slate', 'crystals', 'magma', 'boss')
    for place in places:
        name = 'ambient_' + place
        existing = [os.path.join(OUT, name + ext) for ext in ('.wav', '.ogg', '.mp3')]
        if not force_ambience and any(os.path.exists(p) for p in existing):
            print('Keeping existing ambience:', name)
            continue
        seconds = 18.5
        cutoff = {'surface': 1100, 'house': 250, 'roots': 430, 'slate': 190, 'crystals': 310, 'magma': 170, 'boss': 110}[place]
        bed = noise(seconds, 1e9, cutoff, rng, attack=.001)
        for i in range(len(bed)):
            bed[i] *= .35 + .15 * math.sin(i / RATE * 2 * math.pi / 7)
        if place in ('surface', 'house', 'roots'):
            for i in range(9):
                at = .7 + i * 1.9 + rng.uniform(0, .4)
                if place == 'surface':
                    event = tone(rng.uniform(1700, 2600), .22, .09, attack=.03, sweep=rng.uniform(-.25, .25))
                elif place == 'house':
                    event = tone(rng.uniform(110, 180), .55, .13, ((1, 1), (2.1, .18)), attack=.06, sweep=.04)
                else:
                    event = tone(rng.uniform(760, 1150), .4, .07, attack=.002, sweep=-.3)
                add(bed, event, at, .018 if place == 'house' else .035)
        elif place == 'crystals':
            for i, f in enumerate((523.25, 783.99, 1046.5, 659.25, 1174.66, 783.99)):
                add(bed, tone(f, 2.2, .65, ((1, 1), (2.01, .2)), attack=.12), .5 + i * 2.7, .025)
        else:
            freqs = (45, 67.5) if place == 'slate' else (38, 57) if place == 'magma' else (36, 54, 57)
            for f in freqs:
                add(bed, tone(f, seconds, 1e9, attack=.001), gain=.035)
            if place == 'magma':
                for i in range(16):
                    add(bed, tone(rng.uniform(65, 110), .4, .09, sweep=-.65), .3 + i * 1.05, .08)
            if place == 'boss':
                for i in range(6):
                    add(bed, tone(44, .8, .19, attack=.025), .5 + i * 2.9, .07)
        write(name, seamless(bed), .32 if place != 'boss' else .38, True)


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

    # Jetpack hiss: steady filtered noise whose ends cross-fade so it loops without a click.
    hiss = noise(1.2, 1e9, 1800, rng, attack=0.0001)
    add(hiss, noise(1.2, 1e9, 260, rng, attack=0.0001), gain=1.4)
    blend = int(RATE * 0.2)
    for i in range(blend):
        t = i / blend
        hiss[i] = hiss[i] * t + hiss[len(hiss) - blend + i] * (1 - t)
    write('jet', hiss[:len(hiss) - blend], peak=0.6, loop=True)

    hurt = add(noise(0.3, 0.07, 500, rng), tone(80, 0.3, 0.1, sweep=-0.5), gain=1.2)
    write('hurt', hurt)

    faint = tone(330, 1.2, 0.5, ((1, 1.0), (1.5, 0.3)), attack=0.02, sweep=-0.55)
    add(faint, noise(1.2, 0.5, 200, rng, attack=0.05), gain=0.5)
    write('faint', faint)

    rumble = noise(2.4, 0.9, 140, rng, attack=0.3)
    add(rumble, tone(55, 2.4, 0.9, attack=0.3), gain=0.8)
    write('door', rumble)

    # Dynamite: a crackling fuse and a deep blast with a rolling tail. Appended last so earlier sounds keep their noise.
    fuse = noise(0.7, 0.5, 5200, rng, attack=0.01, highpass=True)
    for i in range(0, len(fuse), int(RATE * 0.045)):
        add(fuse, noise(0.02, 0.006, 3000, rng), at=i / RATE, gain=1.5 * rng.random())
    write('fuse', fuse, peak=0.5)
    boom = noise(1.8, 0.35, 320, rng, attack=0.003)
    add(boom, tone(58, 1.8, 0.45, ((1, 1.0), (2, 0.3)), attack=0.004, sweep=-0.45), gain=1.6)
    add(boom, noise(0.25, 0.05, 2400, rng), gain=0.6)
    add(boom, noise(1.4, 0.6, 120, rng, attack=0.2), at=0.3, gain=0.7)
    write('boom', boom, peak=0.95)


def yard_audio():
    # Its own seed again: sounds above stay byte-for-byte the same.
    rng = random.Random(30092026)
    jump = add(noise(.18, .05, 1500, rng, highpass=True), tone(210, .16, .05, sweep=.6), gain=.5)
    write('jump', jump, .45)
    beep = silence(.02)
    for i in range(2):
        add(beep, tone(1245, .09, .05, ((1, 1), (2, .2))), at=i * .14)
    write('low_fuel', beep, .4)
    # Snoring dog: a slow in-breath hiss and a low buzzing out-breath, looped.
    snore = silence(3.2)
    add(snore, noise(1.1, 1e9, 700, rng, attack=.5), at=.1, gain=.25)
    rattle = tone(62, 1.2, 1e9, ((1, 1), (2, .5), (3, .3)), attack=.25)
    for i in range(len(rattle)):
        rattle[i] *= (.6 + .4 * math.sin(i / RATE * 2 * math.pi * 23)) * min(1, (len(rattle) - i) / (RATE * .4))
    add(snore, rattle, at=1.5, gain=.6)
    write('snore', snore, .5, True)
    croak = silence(.02)
    for i in range(3):
        c = tone(160, .09, .04, ((1, 1), (2, .6), (3, .4), (5, .2)), attack=.008, sweep=-.2)
        for k in range(len(c)):
            c[k] *= .5 + .5 * math.sin(k / RATE * 2 * math.pi * 55)
        add(croak, c, at=i * .11)
    write('croak', croak, .6)
    tweet = silence(.02)
    for i in range(rng.randint(3, 5)):
        add(tweet, tone(rng.uniform(2800, 4200), .08, .03, attack=.005, sweep=rng.uniform(-.4, .4)), at=i * .09 + rng.uniform(0, .03))
    write('tweet', tweet, .45)
    splash = add(noise(.45, .12, 2200, rng), noise(.3, .09, 500, rng), gain=.8)
    write('splash', splash, .55)


# ---------- Polished interface, equipment and digging (overrides the plain first versions above) ----------

def biquad(samples, kind, freq, q=0.7):
    """RBJ biquad: 'low', 'high' or 'band' (constant peak gain)."""
    w = 2 * math.pi * min(freq, RATE * 0.45) / RATE
    cw, sw = math.cos(w), math.sin(w)
    alpha = sw / (2 * q)
    if kind == 'low':
        b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
    elif kind == 'high':
        b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
    else:
        b0, b1, b2 = alpha, 0.0, -alpha
    a0, a1, a2 = 1 + alpha, -2 * cw, 1 - alpha
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
    out, x1, x2, y1, y2 = [], 0.0, 0.0, 0.0, 0.0
    for x in samples:
        y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def white(seconds, rng):
    return [rng.uniform(-1, 1) for _ in range(int(RATE * seconds))]


def shape(samples, attack, decay, hold=0.0):
    """Attack, optional hold, exponential decay."""
    for i in range(len(samples)):
        t = i / RATE
        env = min(1.0, t / attack) if attack > 0 else 1.0
        if t > attack + hold:
            env *= math.exp(-(t - attack - hold) / decay)
        samples[i] *= env
    return samples


def modes(partials, seconds, rng, attack=0.0008):
    """Struck object: decaying sines at given (frequency, decay, gain), random phases."""
    out = [0.0] * int(RATE * seconds)
    for freq, decay, gain in partials:
        phase = rng.uniform(0, 2 * math.pi)
        step = 2 * math.pi * freq / RATE
        for i in range(len(out)):
            t = i / RATE
            out[i] += gain * math.sin(phase + step * i) * min(1.0, t / attack) * math.exp(-t / decay)
    return out


def swept_band(seconds, start, end, q, rng, attack=.02, decay=.1, hold=0.0):
    """Noise through a band-pass whose centre glides from start to end: whooshes and rushes."""
    out, x1, x2, y1, y2 = [], 0.0, 0.0, 0.0, 0.0
    n = int(RATE * seconds)
    for i in range(n):
        k = i / max(1, n - 1)
        f = start * (end / start) ** k
        w = 2 * math.pi * f / RATE
        cw, sw = math.cos(w), math.sin(w)
        alpha = sw / (2 * q)
        a0 = 1 + alpha
        x = rng.uniform(-1, 1)
        y = (alpha * x - alpha * x2 + 2 * cw * y1 - (1 - alpha) * y2) / a0
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return shape(out, attack, decay, hold)


def clicks(seconds, count, rng, low=1800, high=4200, spread=1.0, gain=1.0):
    """Scattered tiny ticks: grit, crumbs, coins bouncing."""
    out = [0.0] * int(RATE * seconds)
    for _ in range(count):
        at = rng.random() ** spread * seconds * .95
        tick = biquad(shape(white(.012, rng), .0003, .0025), 'band', rng.uniform(low, high), 2.5)
        add(out, tick, at, gain * rng.uniform(.3, 1))
    return out


def room(samples, size=.05, feedback=.35, wet=.25):
    """A small space: a few feedback delays, enough to place a sound in a tunnel or a house."""
    out = samples + [0.0] * int(RATE * .4)
    for delay, fb in ((size, feedback), (size * 1.37, feedback * .9), (size * 1.79, feedback * .8)):
        d = int(RATE * delay)
        buf = [0.0] * len(out)
        for i in range(len(out)):
            prev = buf[i - d] if i >= d else 0.0
            buf[i] = (out[i] if i < len(samples) else 0.0) + prev * fb
        for i in range(len(out)):
            out[i] += buf[i] * wet / 3
    return out


def bell(freq, seconds, decay, rng, gain=1.0):
    return modes([(freq, decay, gain), (freq * 2.76, decay * .45, gain * .35), (freq * 5.4, decay * .25, gain * .15)], seconds, rng)


def polished_audio():
    rng = random.Random(1102026)

    # Interface: short, soft and clear, like wooden and brass controls.
    click = modes([(2100, .012, 1), (3350, .008, .5)], .07, rng)
    add(click, biquad(shape(white(.006, rng), .0002, .0015), 'high', 2500), gain=.6)
    add(click, tone(320, .03, .012), gain=.35)
    write('click', click, .42)
    tab = modes([(2650, .009, 1), (4100, .006, .4)], .05, rng)
    add(tab, tone(420, .025, .01), gain=.25)
    write('tab', tab, .32)
    opening = swept_band(.2, 380, 2400, 1.4, rng, .05, .06, .06)
    add(opening, bell(1318.5, .5, .18, rng), .06, 1.4)
    add(opening, bell(1975.5, .5, .2, rng), .12, 1.1)
    write('open', room(opening, .03, .3, .2), .42)
    closing = swept_band(.18, 2000, 450, 1.4, rng, .03, .06, .05)
    add(closing, bell(987.8, .4, .14, rng), .05, 1.2)
    write('close', closing, .36)
    deny = silence(.02)
    for i, f in enumerate((233, 196)):
        add(deny, shape(tone(f, .09, 1e9, ((1, 1), (3, .3), (5, .12))), .004, .05), i * .1)
    write('deny', deny, .42)
    coin = bell(3520, .35, .09, rng)
    add(coin, bell(4410, .3, .06, rng), .012, .6)
    write('coin', coin, .5)
    sell = silence(.02)
    for i in range(16):
        at = (i / 16) ** 1.3 * .55 + rng.uniform(0, .02)
        add(sell, bell(rng.uniform(2600, 4300), .25, rng.uniform(.03, .08), rng), at, rng.uniform(.35, .8))
    add(sell, clicks(.6, 20, rng, 3000, 6000, 1.4, .5))
    for i, f in enumerate((1046.5, 1318.5, 1568)):
        add(sell, bell(f, .6, .25, rng), .5 + i * .06, .9)
    write('sell', room(sell, .035, .3, .18), .6)
    buy = bell(2093, .3, .08, rng)
    add(buy, clicks(.15, 6, rng, 3000, 5000, 1, .5))
    for i, f in enumerate((1318.5, 1760, 2093)):
        add(buy, bell(f, .55, .22, rng), .08 + i * .07, .9)
    write('buy', room(buy, .03, .3, .18), .55)
    upgrade = silence(.02)
    for i in range(7):
        add(upgrade, modes([(1400 + i * 60, .01, 1), (2900, .006, .4)], .03, rng), .02 + i * .045 * (1 - i * .06), .8)
        add(upgrade, tone(180, .02, .008), .02 + i * .045 * (1 - i * .06), .3)
    for i, f in enumerate((784, 1046.5, 1318.5, 1568, 2093)):
        add(upgrade, bell(f, .8, .32, rng), .32 + i * .075, 1)
    add(upgrade, swept_band(.7, 1500, 6000, 3, rng, .2, .25), .3, .25)
    write('upgrade', room(upgrade, .04, .35, .22), .6)
    pickup = silence(.01)
    add(pickup, bell(1568, .3, .09, rng), 0, 1)
    add(pickup, bell(2349, .35, .12, rng), .06, 1)
    add(pickup, clicks(.2, 5, rng, 4000, 7000, 1, .25), .05)
    write('pickup', pickup, .5)

    # Equipment.
    swing = swept_band(.24, 320, 1300, 1.8, rng, .07, .05, .05)
    write('swing', swing, .4)
    jet = silence(2.6)
    add(jet, biquad(white(2.6, rng), 'low', 160, .8), gain=1.4)
    add(jet, biquad(white(2.6, rng), 'band', 700, .9), gain=.55)
    add(jet, biquad(white(2.6, rng), 'high', 2600, .7), gain=.35)
    for i in range(len(jet)):
        jet[i] *= .82 + .18 * math.sin(i / RATE * 2 * math.pi * 17) + .06 * math.sin(i / RATE * 2 * math.pi * 3.1)
    write('jet', seamless(jet, .4), .55, True)
    start = shape(tone(95, .35, 1e9, ((1, 1), (2, .4)), sweep=-.4), .003, .08)
    add(start, biquad(shape(white(.25, rng), .002, .05), 'low', 900), gain=.9)
    add(start, swept_band(.45, 500, 2200, 1.2, rng, .12, .12), .05, .7)
    write('jet_start', start, .6)
    motor = silence(1.6)
    for h, g in ((1, 1), (2, .6), (3, .45), (4, .25), (6, .12)):
        add(motor, tone(58 * h, 1.6, 1e9, attack=.001), gain=g * .5)
    grind = biquad(white(1.6, rng), 'band', 1900, 1.1)
    for i in range(len(motor)):
        t = i / RATE
        motor[i] = motor[i] * (.75 + .25 * math.sin(t * 2 * math.pi * 29)) + grind[i] * (.5 + .5 * math.sin(t * 2 * math.pi * 11)) * .9
    write('drill_loop', seamless(motor, .3), .5, True)
    bite = biquad(shape(white(.12, rng), .002, .04), 'band', 1100, 1.3)
    add(bite, clicks(.12, 14, rng, 1500, 4500, 1, .6))
    add(bite, shape(tone(110, .08, 1e9, sweep=-.3), .002, .03), gain=.6)
    write('drill', bite, .55)
    harpoon = modes([(330, .12, 1), (660, .07, .4), (990, .04, .2)], .4, rng)
    add(harpoon, swept_band(.3, 2400, 600, 1.5, rng, .01, .08), .02, 1.2)
    add(harpoon, biquad(shape(white(.02, rng), .0003, .004), 'high', 1500), gain=.8)
    add(harpoon, shape(tone(70, .12, 1e9), .002, .04), .01, .7)
    write('harpoon', harpoon, .75)
    scan = silence(.02)
    for i in range(4):
        ping = shape(tone(1500, .3, 1e9, ((1, 1), (2.01, .2)), sweep=-.08), .004, .09)
        add(scan, ping, i * .19, .9 * .5 ** i)
    add(scan, swept_band(.5, 600, 3000, 2.5, rng, .15, .2), 0, .25)
    write('scan', room(scan, .06, .4, .25), .5)
    heal = silence(.02)
    for i, f in enumerate((1046.5, 1318.5, 1568, 2093)):
        add(heal, bell(f, .6, .2, rng), i * .07, .8)
    add(heal, swept_band(.6, 1200, 5000, 2, rng, .15, .2), 0, .35)
    write('heal', heal, .5)

    # Digging: a soft thump and crumbling soil; stone that knocks and rings; crystal that chimes; heavy basalt.
    for v in range(3):
        dirt = shape(tone(rng.uniform(80, 100), .12, 1e9, sweep=-.35), .002, .045)
        add(dirt, biquad(shape(white(.2, rng), .003, .06), 'low', rng.uniform(900, 1300), .8), gain=1.2)
        add(dirt, clicks(.3, 26, rng, 900, 3200, 1.6, .55), .01)
        add(dirt, clicks(.35, 8, rng, 700, 2000, 1, .25), .12)
        write('dig_dirt' + ('' if v == 0 else '_%d' % (v + 1)), room(dirt, .045, .25, .15), .8)
    for v in range(3):
        base = rng.uniform(900, 1300)
        rock = biquad(shape(white(.008, rng), .0002, .002), 'high', 2000)
        add(rock, modes([(base, .05, 1), (base * 1.63, .035, .6), (base * 2.42, .025, .35), (base * 3.1, .015, .2)], .2, rng), gain=.8)
        add(rock, shape(tone(125, .1, 1e9), .002, .03), gain=.7)
        add(rock, clicks(.4, 12, rng, 1500, 4000, 1.3, .4), .05)
        write('dig_stone' + ('' if v == 0 else '_%d' % (v + 1)), room(rock, .05, .35, .2), .85)
    for v in range(3):
        base = rng.uniform(2300, 3000)
        glass = modes([(base, .25, 1), (base * 1.47, .18, .55), (base * 2.13, .12, .35), (base * 2.95, .08, .2)], .55, rng)
        add(glass, biquad(shape(white(.05, rng), .0005, .012), 'high', 3000), gain=.5)
        add(glass, clicks(.3, 10, rng, 3500, 7000, 1.2, .35), .03)
        add(glass, shape(tone(160, .08, 1e9), .002, .025), gain=.4)
        write('dig_crystal' + ('' if v == 0 else '_%d' % (v + 1)), room(glass, .06, .4, .25), .75)
    for v in range(2):
        heavy = shape(tone(68, .2, 1e9, ((1, 1), (2, .3)), sweep=-.3), .002, .06)
        base = rng.uniform(480, 650)
        add(heavy, modes([(base, .06, .8), (base * 1.8, .04, .5), (base * 2.7, .03, .3)], .2, rng), gain=.7)
        add(heavy, biquad(shape(white(.12, rng), .002, .035), 'band', 800, 1), gain=.8)
        add(heavy, clicks(.4, 10, rng, 900, 2600, 1.4, .45), .04)
        write('dig_basalt' + ('' if v == 0 else '_%d' % (v + 1)), room(heavy, .06, .4, .25), .85)
    bounce = modes([(820, .18, 1), (1640, .1, .4), (2710, .06, .25)], .4, rng)
    for i in range(len(bounce)):
        bounce[i] *= .7 + .3 * math.sin(i / RATE * 2 * math.pi * 38)
    add(bounce, biquad(shape(white(.01, rng), .0002, .003), 'high', 2000), gain=.6)
    write('dig_bounce', bounce, .6)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--force-ambience', action='store_true', help='Regenerate temporary ambience WAVs, replacing existing WAVs.')
    args = parser.parse_args()
    main()
    new_audio(args.force_ambience)
    yard_audio()
    polished_audio()
