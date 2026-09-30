"""Package one compressed Unity build with six isolated portal integrations."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PLATFORMS = {
    'CrazyGames': ('https://sdk.crazygames.com/crazygames-sdk-v3.js', True, False,
                  'https://docs.crazygames.com/sdk/intro/'),
    'Poki': ('https://game-cdn.poki.com/scripts/v2/poki-sdk.js', True, True,
             'https://developers.poki.com/guide/sdk-html5'),
    'GameDistribution': ('https://html5.api.gamedistribution.com/main.min.js', True, True,
                         'https://github.com/GameDistribution/GD-HTML5/wiki'),
    'GameMonetize': ('https://api.gamemonetize.com/sdk.js', False, True,
                     'https://github.com/MonetizeGame/GameMonetize.com-SDK'),
    'GamePix': ('https://integration.gamepix.com/sdk/v3/gamepix.sdk.js', True, True,
                'https://partners.gamepix.com/sdk/doc/javascript'),
    'YandexGames': ('/sdk.js', True, True,
                    'https://yandex.ru/dev/games/doc/ru/sdk/sdk-about'),
}

NOTES = {
    'CrazyGames': 'Загрузить в Developer Portal и проверить в Preview. Реклама после 240 секунд активной игры запрашивается при возвращении из шахты на поверхность, а не на кнопках меню/магазина. Поддержан muteAudio площадки. На Basic Launch площадка отключает рекламу. Перед финальным релизом нужна английская локализация.',
    'Poki': 'Пакет для Poki Inspector и заявки. Poki просит веб-эксклюзивность: https://developers.poki.com/guide/working-with-poki . Публикация параллельно на других сайтах требует согласования с Poki. Перед релизом нужна английская локализация.',
    'GameDistribution': 'В кабинете создать игру, скопировать gameId, включить Rewarded Ads. Подставить ID и пересобрать архив. Текущая версия запрашивает mid-roll после 240 секунд на паузе; pre-roll при первом запуске не добавлен. Перед финальной отправкой согласовать это с площадкой и подготовить английскую локализацию.',
    'GameMonetize': 'В кабинете создать игру, скопировать GameId, подставить его и пересобрать архив. Затем выполнить Verify Game. Подключён showBanner, пауза и возобновление по событиям SDK. Кнопка рекламы за монеты скрыта: публичный SDK не документирует подтверждение rewarded-просмотра. Бесплатных наград нет.',
    'GamePix': 'Загрузить и проверить через инструменты GamePix. Подключены loading/loaded и оба вида рекламы. Перед финальной отправкой нужна английская локализация. Сохранения пока используют Unity PlayerPrefs/IndexedDB; перенос на GamePix.localStorage не выполнен, облачные сохранения не заявлены.',
    'YandexGames': 'Загрузить ZIP в черновик Яндекс Игр. /sdk.js предоставляется площадкой; отдельный ID в коде не требуется. Есть LoadingAPI, GameplayAPI, rewarded/interstitial, события паузы. Проверить в черновике на телефоне и ПК.',
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def transform(html, name, sdk_url):
    start = html.index('    // The platform serves /sdk.js;')
    end = html.index("    const canvas = document.getElementById('unity-canvas');", start)
    html = html[:start] + '    window.nubikReady = false;\n    window.nubikPlaying = false;\n' + html[end:]
    # Remove the standalone test-ad styling too: no test ad exists in any upload archive.
    html = re.sub(r'^    #fake-ad[^\n]*\n', '', html, flags=re.M)
    scripts = ''
    if name not in ('GameDistribution', 'GameMonetize'):
        scripts = (f'  <script id="nubik-portal-sdk" async src="{sdk_url}" '
                   'onload="this.dataset.loaded=\'1\'" onerror="this.dataset.failed=\'1\'"></script>\n')
    scripts += '  <script src="platform-config.js"></script>\n  <script src="platform.js"></script>\n'
    html = html.replace('<head>\n', '<head>\n' + scripts, 1)
    old = "value => document.getElementById('progress').value = value"
    assert old in html, 'Unity progress callback changed'
    html = html.replace(old, "value => { document.getElementById('progress').value = value; window.nubikPortal.progress(value); }")
    html = html.replace('window.unityInstance = instance;', 'window.unityInstance = instance;\n      window.nubikPortal.attach();', 1)
    assert 'nubikFakeAd' not in html
    return html


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, default=ROOT / 'Builds/PlatformsBase')
    parser.add_argument('--output', type=Path, default=ROOT / 'Builds/Platforms')
    parser.add_argument('--ids', type=Path)
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    if output.exists():
        raise SystemExit('Output already exists; choose a fresh --output directory. Existing builds are kept.')
    if source == output or source in output.parents or output in source.parents:
        raise SystemExit('Source and output must be separate directories')
    html = (source / 'index.html').read_text(encoding='utf-8-sig')
    assert list((source / 'Build').glob('*.unityweb')), 'BuildPlatformsBase must use gzip + decompression fallback'
    ids = json.loads(args.ids.read_text(encoding='utf-8-sig')) if args.ids else {}
    for key, value in ids.items():
        if key not in ('GameDistribution', 'GameMonetize') or not isinstance(value, str):
            raise SystemExit('IDs file must contain string IDs for GameDistribution / GameMonetize')
        if value and not re.fullmatch(r'[A-Za-z0-9_-]{8,128}', value):
            raise SystemExit(f'Invalid {key} game ID')
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    dirty = bool(subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True).strip())
    output.mkdir(parents=True)
    rows = []
    for name, (sdk_url, rewarded, menu_ads, docs) in PLATFORMS.items():
        folder = output / name
        web = folder / 'WebGL'
        web.mkdir(parents=True)
        for directory in ('Build', 'StreamingAssets'):
            shutil.copytree(source / directory, web / directory)
        config = dict(platform=name, sdkUrl=sdk_url, gameId=ids.get(name, ''), rewarded=rewarded, menuAds=menu_ads)
        (web / 'platform-config.js').write_text('window.NUBIK_PLATFORM = ' + json.dumps(config, indent=2) + ';\n', encoding='utf-8')
        shutil.copyfile(ROOT / 'Tools/Platforms/portal.js', web / 'platform.js')
        (web / 'index.html').write_text(transform(html, name, sdk_url), encoding='utf-8')
        missing_id = name in ('GameDistribution', 'GameMonetize') and not config['gameId']
        manifest = dict(platform=name, sourceCommit=revision, sourceDirty=dirty,
                        status='needs-game-id' if missing_id else 'prepared-for-platform-preview',
                        realPortalTested=False, language='ru', compression='gzip-unity-fallback',
                        files={p.relative_to(web).as_posix(): sha(p) for p in sorted(web.rglob('*')) if p.is_file()})
        (folder / 'manifest.json').write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding='utf-8')
        archive = folder / f'Nubik-{name}.zip'
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as zipped:
            for file in sorted(web.rglob('*')):
                if file.is_file(): zipped.write(file, file.relative_to(web).as_posix())
        with zipfile.ZipFile(archive) as zipped:
            assert zipped.testzip() is None
            assert 'index.html' in zipped.namelist()
            assert len(zipped.namelist()) == len(manifest['files'])
            for relative, expected in manifest['files'].items():
                assert hashlib.sha256(zipped.read(relative)).hexdigest() == expected
        status = 'НУЖЕН GAME ID — реклама пока отключена' if missing_id else 'Подготовлено для проверки в кабинете площадки'
        readme = f'''# Нубик Шахтёр — {name}

**{status}.** Это сборка текущей русской версии, не подтверждение прохождения модерации.

Архив для загрузки: `Nubik-{name}.zip`. `index.html` уже лежит в корне ZIP.
`WebGL/` — распакованная копия. Загружайте только содержимое архива, не всю папку с README.
Unity gzip с распаковкой в загрузчике: специальных Content-Encoding заголовков не требуется.
Все игровые ресурсы и музыка внутри архива. Только SDK выбранной площадки загружается извне.

{NOTES[name]}

SDK: {docs}

ID и настройки: `WebGL/platform-config.js`. Для повторной упаковки обеих площадок с ID
скопируйте `Tools/Platforms/ids.example.json`, заполните значения и из корня проекта выполните:
`py -3 Tools/package_platforms.py --ids ПУТЬ_К_ids.json --output Builds/Platforms-with-ids`
Редактирование распакованного файла само по себе не обновляет ZIP.

Для локального запуска нужен HTTP-сервер; открытие index.html двойным кликом (file://) не поддерживается Unity.
Без доступного SDK игра работает без рекламы. Заглушек с выдачей монет в пакетах нет.
После загрузки в кабинет проверить показ, отказ/пропуск рекламы, возврат звука и управления,
сохранение прогресса после обновления, горизонтальный режим iPhone/Android.
Реальная выдача рекламы проверяется только на площадке, после настройки аккаунта.

Исходная версия: `{revision}`; незакоммиченные изменения: `{dirty}`.
ZIP SHA-256: `{sha(archive)}`. Контрольные суммы файлов: `manifest.json`.
'''
        (folder / 'README.md').write_text(readme, encoding='utf-8-sig')
        rows.append(dict(platform=name, zip=archive.relative_to(output).as_posix(), bytes=archive.stat().st_size,
                         sha256=sha(archive), status=manifest['status']))
        print(f'{name}: {archive.stat().st_size / 1048576:.1f} MiB; {manifest["status"]}', flush=True)
    (output / 'manifest.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
    summary = '# Сборки «Нубик Шахтёр» для площадок\n\n'
    summary += '\n'.join(f'- [{r["platform"]}]({r["platform"]}/README.md): [{Path(r["zip"]).name}]({r["zip"]}) — {r["status"]}' for r in rows)
    summary += '\n\nВ каждой папке ZIP, WebGL, README и контрольные суммы. Все ZIP проверены по CRC и SHA-256.\n'
    summary += '\nДля GameDistribution и GameMonetize нужны ID из ваших кабинетов. Подставных ID нет.\n'
    summary += '\nВерсии для предварительной проверки: международная локализация пока не сделана. У GamePix сохранения пока Unity IndexedDB, у GameDistribution нет pre-roll. Подробнее — в README соответствующей площадки.\n'
    summary += '\nPoki требует веб-эксклюзивности: https://developers.poki.com/guide/working-with-poki . Пакеты на сайты не отправлялись.\n'
    (output / 'README.md').write_text(summary, encoding='utf-8-sig')


if __name__ == '__main__':
    main()
