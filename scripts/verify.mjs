import { spawnSync } from 'node:child_process';
import { mkdirSync, readdirSync, readFileSync, rmSync } from 'node:fs';
const run = (command, args, options = {}) => {
  const result = spawnSync(command, args, { stdio: 'inherit', ...options });
  if (result.error || result.status !== 0) { console.error(result.error ?? `Failed: ${command}`); process.exit(result.status || 1); }
};
// Use the npm CLI shipped alongside the invoking npm, including Windows installations.
const front = (args, env = process.env) => run(process.execPath, [process.env.npm_execpath, ...args], { cwd: 'frontend', env });
run('ffmpeg', ['-version']);
run('ffprobe', ['-version']);
run('dotnet', ['restore', 'backend/DjVisualizer.sln', '--locked-mode']);
run(process.execPath, ['scripts/architecture.mjs']);
run(process.execPath, ['--test', 'scripts/gates.test.mjs']);
front(['run', 'lint']);
run(process.execPath, ['scripts/format.mjs']);
run('dotnet', ['build', 'backend/DjVisualizer.sln', '--no-restore', '-c', 'Release']);
const results = '.verification';
rmSync(results, { recursive: true, force: true });
mkdirSync(results);
run('dotnet', ['test', 'backend/DjVisualizer.sln', '--no-build', '-c', 'Release', '--logger', 'trx', '--results-directory', results]);
const reports = readdirSync(results).filter(x => x.endsWith('.trx'));
if (reports.length !== 5 || reports.some(x => /outcome="NotExecuted"/.test(readFileSync(`${results}/${x}`, 'utf8')))) {
  throw new Error('All five backend suites must run without skipped tests.');
}
front(['test']);
front(['run', 'build']);
front(['run', 'build', '--', '--mode', 'standalone'], { ...process.env, VITE_SAMPLE_ENABLED: 'true' });
console.log('verify passed: architecture, lint, formatting, typed builds and all tests.');
