"""Guard tests for publication boundaries, private data and cache identity."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import prepare_vercel as pack

HTML = '''<!DOCTYPE html><html><head><link rel="stylesheet" href="TemplateData/style.css"></head><body><script>
var buildUrl = "Build";
var loaderUrl = buildUrl + "/sample.loader.js";
var config = {dataUrl: buildUrl + "/sample.data.br", frameworkUrl: buildUrl + "/sample.framework.js.br", codeUrl: buildUrl + "/sample.wasm.br"};
config.autoSyncPersistentDataPath = true;
createUnityInstance(canvas, config, () => {}).then((unityInstance) => {});
</script></body></html>'''

def compress(path, data):
    subprocess.run(['node', '-e', 'require("node:fs").writeFileSync(process.argv[1], require("node:zlib").brotliCompressSync(Buffer.from(process.argv[2],"base64")))', str(path), __import__('base64').b64encode(data).decode()], check=True)

class PublicationGuards(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(); self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name); self.source = self.root / 'web'; self.output = self.root / 'deploy'
        (self.source / 'Build').mkdir(parents=True); (self.source / 'TemplateData').mkdir()
        (self.source / 'index.html').write_text(HTML)
        (self.source / 'TemplateData/style.css').write_text('body { background: black; }')
        (self.source / 'Build/sample.loader.js').write_text('/* loader */')
        for name, data in [('data', b'game resources'), ('framework.js', b'/* framework */'), ('wasm', b'\x00asm\x01\x00\x00\x00')]: compress(self.source / ('Build/sample.' + name + '.br'), data)
    def build(self): return pack.package(self.source, self.output, 'guard-test')
    def test_only_runtime_files_are_public(self):
        (self.source / 'TemplateData/unused.png').write_bytes(b'not needed')
        result = self.build()
        self.assertEqual(len(result['files']), 6)
        self.assertFalse((self.output / 'public/TemplateData/unused.png').exists())
        self.assertFalse((self.output / 'public/release-manifest.json').exists())
        self.assertNotIn(str(self.root), json.dumps(result))
    def test_unity_project_source_is_refused(self):
        (self.source / 'Assets').mkdir(); (self.source / 'Assets/Secret.cs').write_text('private source')
        with self.assertRaisesRegex(ValueError, 'release-only'): self.build()
        self.assertFalse((self.output / 'public').exists())
    def test_private_path_inside_brotli_is_refused(self):
        compress(self.source / 'Build/sample.data.br', b'private /Users/isao/Documents/profile/save.json')
        with self.assertRaisesRegex(ValueError, 'content withheld'): self.build()
    def test_real_save_json_inside_brotli_is_refused(self):
        compress(self.source / 'Build/sample.data.br', b'{"instanceId":"character-0123456789abcdef0123456789abcdef"}')
        with self.assertRaisesRegex(ValueError, 'content withheld'): self.build()
    def test_disabled_persistence_is_refused(self):
        (self.source / 'index.html').write_text(HTML.replace('config.autoSyncPersistentDataPath = true;', '// config.autoSyncPersistentDataPath = true;'))
        with self.assertRaisesRegex(ValueError, 'persistence'): self.build()
    def test_symlinks_are_refused(self):
        (self.source / 'TemplateData/link.css').symlink_to(self.source / 'index.html')
        with self.assertRaisesRegex(ValueError, 'symlinks'): self.build()
    def test_changed_loader_gets_a_new_url_without_changing_game_data(self):
        before = self.build(); (self.source / 'Build/sample.loader.js').write_text('/* updated loader */'); after = self.build()
        old = {n for n in before['files'] if n.endswith('.loader.js')}; new = {n for n in after['files'] if n.endswith('.loader.js')}
        self.assertNotEqual(old, new)
        self.assertEqual({n for n in before['files'] if n.endswith('.br')}, {n for n in after['files'] if n.endswith('.br')})
        self.assertNotEqual(before['release'], after['release'])
        pack.validate(self.output)
    def test_modified_public_payload_is_refused(self):
        result = self.build(); name = next(n for n in result['files'] if n.endswith('.data.br'))
        (self.output / 'public' / name).write_bytes(b'tampered')
        with self.assertRaisesRegex(ValueError, 'digest'): pack.validate(self.output)

if __name__ == '__main__': unittest.main()
