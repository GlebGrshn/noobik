# Пакеты для игровых площадок

Подготовлены адаптеры CrazyGames, Poki, GameDistribution, GameMonetize, GamePix и Яндекс Игр.
Это пакеты двуязычной версии (RU/EN) для предварительной проверки в кабинетах, а не подтверждение допуска к публикации.
Отправка на площадки и оформление партнёрств не выполнялись.

## Как собрать

1. Unity 6000.6.3f1: выполнить `ProjectSetup.BuildPlatformsBase` в batchmode.
   Результат — `Builds/PlatformsBase`, gzip с Unity decompression fallback.
2. Выполнить `node Tools/check-platforms.mjs` и `node Tools/check-ad-bridge.mjs`.
3. Выполнить `py -3 Tools/package_platforms.py`.
   Результат — `Builds/Platforms/<площадка>/`: `WebGL`, ZIP, README и SHA-256 manifest.
   В корне каждого ZIP находится `index.html`; документация остаётся снаружи архива.
4. Для повторного запуска указать новую папку `--output Builds/Platforms-next`.
   Скрипт намеренно не удаляет предыдущие сборки.

Для GameDistribution и GameMonetize необходимы ID из личных кабинетов. Скопировать
`Tools/Platforms/ids.example.json`, заполнить значения и передать `--ids путь/ids.json`.
Пустые ID не заменяются чужими или демонстрационными: игра запускается, реклама отключена.
Изменение `WebGL/platform-config.js` не меняет уже созданный ZIP — его требуется перепаковать.

Один Unity WebGL binary используется для всех площадок; различаются HTML-загрузчик и конфигурация SDK.
`Tools/Platforms/portal.js` преобразует callbacks площадок в существующие сообщения `YandexBridge`.
Имя C#-компонента оставлено для совместимости. В каждом пакете загружается SDK только своей площадки.
Музыка и игровые ресурсы локальные. Старый шаблон GitHub Pages остаётся отдельным; в портальных пакетах тестовой рекламы нет.

## Поведение

- Награда выдаётся один раз, исключительно по подтверждению SDK. Закрытие interstitial ничего не начисляет.
- На время запроса рекламы блокируется ввод canvas; Unity останавливает игру и звук. Ошибка возвращает управление.
- Запрос, который не начал показ за 30 секунд, отменяется без награды. Для уже открытой рекламы этот таймер отключается.
- События готовности и начала игры ждут инициализации SDK и не дублируются.
- Интервал — 240 секунд активной игры. Обычные пакеты используют открытие паузы/дома;
  CrazyGames — завершение вылазки после приземления на поверхности, поскольку меню/магазин не разрешены как midgame placement.
- CrazyGames: SDK v3, loading/gameplay, rewarded/midgame, настройки `muteAudio`.
- Poki: загрузка, gameplay, commercial/rewarded break; результат `false` не даёт награды.
- GameDistribution: `GD_OPTIONS`, ожидание `SDK_READY`, pause/start, `showAd`, награда по `SDK_REWARDED_WATCH_COMPLETE`.
- GameMonetize: `SDK_OPTIONS`, ожидание `SDK_READY`, `showBanner`, pause/start. Кнопка rewarded скрыта,
  поскольку публичная документация не описывает отдельный completed-reward контракт.
- GamePix: loading/loaded, interstitial/rewardAd, успех награды только при `res.success === true`.
- Яндекс: LoadingAPI, GameplayAPI, реклама и platform pause/resume; язык — из `environment.i18n.lang` без окна выбора (требование 2.14, см. Localization.md).

## Что проверить перед публикацией

| Площадка | Оставшиеся условия |
| --- | --- |
| CrazyGames | Preview, условия перехода из Basic Launch в монетизацию |
| Poki | Inspector, согласование веб-эксклюзивности |
| GameDistribution | gameId, Rewarded Ads flag, проверка в кабинете; текущая игра без pre-roll, требуется согласование или добавление перед релизом |
| GameMonetize | GameId, Verify Game, проверка реального showBanner |
| GamePix | Проверка в кабинете, сейчас сохранения Unity PlayerPrefs/IndexedDB, интеграция GamePix.localStorage не выполнена |
| Яндекс Игры | Черновик, реальные рекламные callbacks, мобильная проверка и модерация |

Адаптеры не обеспечивают облачные сохранения. Русский и английский включены: первый запуск предлагает выбор, настройки позволяют переключать язык. Подробности — в Localization.md.
На каждом сайте проверить сохранение после перезагрузки, отказ/пропуск/успех рекламы, фон вкладки,
поворот телефона, звук, чувствительность камеры и возврат управления. Настоящий показ рекламы
не подтверждается локальными тестами и требует кабинета площадки.

Проверка этой версии: 94 EditMode + 21 PlayMode; 31 сценарий адаптеров с mock SDK и 6 сценариев исходного JS bridge.
Архиватор проверяет CRC, корневой index.html и SHA-256 каждого вложенного файла.
Все шесть вариантов запущены в headless Edge через HTTP до `NubikReady`, открытия игры и дома.
CrazyGames, Poki и GamePix SDK инициализировались локально; два варианта без ID и Яндекс без
платформенного `/sdk.js` корректно продолжили игру с отключённой рекламой. Ошибок исполнения
не было; остаются прежние предупреждения URP и локальное предупреждение consent SDK Poki.
Кадры и журнал — `TestResults/Portals`. Проверка не означает показ реальной рекламы на порталах.

## Первичные источники

- [CrazyGames SDK](https://docs.crazygames.com/sdk/intro/), [реклама](https://docs.crazygames.com/sdk/video-ads/), [размещение рекламы](https://docs.crazygames.com/requirements/ads/), [muteAudio](https://docs.crazygames.com/sdk/game/).
- [Poki HTML5 SDK](https://developers.poki.com/guide/sdk-html5), [веб-эксклюзивность](https://developers.poki.com/guide/working-with-poki).
- [GameDistribution SDK](https://github.com/GameDistribution/GD-HTML5/wiki/SDK-Implementation), [rewarded](https://github.com/GameDistribution/GD-HTML5/wiki/Rewarded-Ads).
- [GameMonetize SDK](https://github.com/MonetizeGame/GameMonetize.com-SDK).
- [GamePix JavaScript SDK](https://partners.gamepix.com/sdk/doc/javascript).
- [Яндекс SDK](https://yandex.ru/dev/games/doc/ru/sdk/sdk-about).
