"""Check localization data without changing Unity assets or player data."""
from pathlib import Path
import json, re
root = Path(__file__).resolve().parents[1]
entries = json.loads((root / 'Assets/Resources/Data/ui_translations.json').read_text())['entries']
keys = set()
for entry in entries:
    key = entry.get('key')
    assert key and key not in keys, f'Duplicate/missing translation key: {key}'
    keys.add(key)
    for language in ('en', 'ja'):
        assert entry.get(language), f'Missing {language}: {key}'
    parameters = lambda value: sorted(re.findall(r'\{[0-9]+\}', value))
    assert parameters(entry['en']) == parameters(entry['ja']), f'Parameter mismatch: {key}'
for script in (root / 'Assets/Scripts').glob('*.cs'):
    for key in re.findall(r'"((?:ui|settings|stat)\.[a-zA-Z0-9_.]+)"', script.read_text()):
        assert key == 'settings.json' or key in keys, f'{script.name}: missing {key}'
for line in json.loads((root / 'Assets/Resources/Data/dialogues.json').read_text())['dialogues']:
    key = 'dialogue.' + line['dialogueId']
    assert key in keys, f'Missing dialogue translation: {key}'
    entry = next(e for e in entries if e['key'] == key)
    expected = line['text'].replace('{skill}', '{0}').replace('{enemy}', '{1}')
    assert entry['en'] == expected, f'Dialogue source changed without updating translations: {key}'
print(f'PASS: {len(entries)} Japanese/English entries; keys, parameters, runtime references and dialogue sources validated.')
