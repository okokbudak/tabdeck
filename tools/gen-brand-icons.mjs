// brand-icons.txt listesindeki Simple Icons (CC0) logolarini uygulamanin
// assets klasorune tek bir JSON olarak yazar: [{slug, title, hex, path}]
// Kullanim: node gen-brand-icons.mjs <liste.txt> <cikti.json>
import * as si from 'simple-icons';
import fs from 'node:fs';

const [, , listFile, outFile] = process.argv;
if (!listFile || !outFile) {
  console.error('kullanim: node gen-brand-icons.mjs <liste.txt> <cikti.json>');
  process.exit(1);
}

const wanted = fs.readFileSync(listFile, 'utf8')
  .split(/\r?\n/)
  .map((s) => s.replace(/#.*/, '').trim())
  .filter(Boolean);

const bySlug = new Map(
  Object.values(si).filter((x) => x && typeof x === 'object' && x.slug).map((x) => [x.slug, x]),
);

const out = [];
const missing = [];
const seen = new Set();
for (const slug of wanted) {
  if (seen.has(slug)) continue;
  seen.add(slug);
  const icon = bySlug.get(slug);
  if (!icon) {
    missing.push(slug);
    continue;
  }
  out.push({ slug: icon.slug, title: icon.title, hex: icon.hex, path: icon.path });
}
out.sort((a, b) => a.title.localeCompare(b.title, 'tr'));

fs.writeFileSync(outFile, JSON.stringify(out));
console.log(`toplam simple-icons: ${bySlug.size}`);
console.log(`istenen: ${seen.size}, bulunan: ${out.length}, eksik: ${missing.length}`);
console.log(`eksik: ${missing.join(' ')}`);
console.log(`boyut: ${(fs.statSync(outFile).size / 1024).toFixed(0)} KB`);
