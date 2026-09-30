# Рекламный ролик «Нубик Шахтёр» (Remotion)

Четыре версии для рекламы из одного монтажа (`src/promo/timeline.ts`):

| Композиция | Формат | Длина |
|---|---|---|
| Promo30-Landscape | 1920 × 1080, 16:9 | 30 с |
| Promo30-Portrait | 1080 × 1920, 9:16 | 30 с |
| Promo15-Landscape | 1920 × 1080, 16:9 | 15 с |
| Promo15-Portrait | 1080 × 1920, 9:16 | 15 с |

Сюжет: двор → спуск через корни, штольню, кристаллы и магму (плашки глубины как в игре) → дом, продажа и улучшения → взрыв метеорита → Ктулху → победа → карточка «Нубик Шахтёр · Копай глубже · Играть бесплатно». Склейки стоят на долях музыки (оба трека около 152 BPM, половина такта 0,79 с). Музыка — треки игры: «Digging in the Dirt» с 80,5 с до появления Ктулху, затем «Subterranean Pressure» с 174,3 с. Звуки — из `Assets/Game/Resources/Audio`. Шрифт Rubik (Google Fonts, кириллица).

## Медиа

`public/media` не хранится в Git — его собирают из проекта:

- `clips/` — пролёты `Recordings/promo-*/0*.mp4` (`PromoRecorder`, см. `Docs/Promo.md`);
- `stills/06_house_buyer.jpg`, `stills/07_house_workshop.jpg` — 4K-скриншоты `PromoStills`, уменьшенные до 2880 × 1620;
- `music/` — `music_2_roots.mp3`, `music_4_boss.mp3` из `Assets/StreamingAssets/Music`;
- `sfx/` — `swing dig_dirt dig_stone dig_crystal sizzle coin upgrade fuse boom roar harpoon boss_hit zone` (`.wav`).

## Работа

```bash
npm i
npm run dev                                   # Remotion Studio, http://localhost:3000
npx remotion render Promo30-Landscape out/Promo30-Landscape.mp4 --codec=h264 --crf=16 --audio-bitrate=256k
node scripts/frames.mjs Promo30-Portrait out/frames 40 180 575   # отдельные кадры для проверки
```

Remotion закреплён на **4.0.530**: в опубликованной 4.0.531 файл `@remotion/cli/dist/render-queue/queue.js` пустой, и Studio падает с `getRenderQueue is not a function`. Не запускать `npm run upgrade`, пока это не исправят; версии всех пакетов `remotion` и `@remotion/*` должны совпадать.

Лицензия Remotion: бесплатно для частных лиц и команд до 3 человек (https://www.remotion.pro/license).
