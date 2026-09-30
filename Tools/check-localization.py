"""Fail if a Russian runtime string is added without an English translation."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
catalog = json.loads((root / 'Assets/Game/Resources/Localization/English.json').read_text(encoding='utf-8'))['entries']
translations = {entry['ru']: entry['en'] for entry in catalog}
assert len(translations) == len(catalog), 'Duplicate Russian catalog keys'
assert all(value.strip() and not re.search('[А-Яа-яЁё]', value) for value in translations.values())
# These two strings deliberately identify Russian in its own language on the bilingual chooser.
bilingual = {'Русский', 'Выберите язык'}
missing = []
for path in (root / 'Assets/Game/Runtime').glob('*.cs'):
    for match in re.finditer(r'"(?:\\.|[^"\\])*"', path.read_text(encoding='utf-8-sig')):
        if not re.search('[А-Яа-яЁё]', match.group()):
            continue
        value = json.loads(match.group())
        if value not in translations and value not in bilingual:
            missing.append(f'{path.name}: {value}')
if missing:
    raise SystemExit('Missing translations:\n' + '\n'.join(missing))
print(f'Localization coverage passed: {len(translations)} entries, no missing runtime strings.')
