// Run from repository root: node tasks/REPO-01/verify-links.mjs
import { execFileSync } from 'node:child_process';
import { readFileSync, existsSync } from 'node:fs';
import { dirname, resolve } from 'node:path';

const files = execFileSync('rg', ['--files', '-g', '*.md', '-g', '!tasks/*/local/**'], { encoding: 'utf8' }).trim().split(/\r?\n/);
const broken = [];
let links = 0;
for (const file of files) {
  for (const match of readFileSync(file, 'utf8').matchAll(/\[[^\]]*\]\(([^)]+)\)/g)) {
    const target = match[1];
    if (/^(?:[a-z]+:|#)/i.test(target)) continue;
    links++;
    if (!existsSync(resolve(dirname(file), target.split('#')[0]))) broken.push(`${file}: ${target}`);
  }
}
if (broken.length) {
  process.stderr.write(broken.join('\n') + '\n');
  process.exitCode = 1;
} else {
  process.stdout.write(`${links} local documentation links verified across ${files.length} files.\n`);
}
