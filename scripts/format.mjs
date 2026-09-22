import { spawnSync } from 'node:child_process';
for (const [command, args] of [
  ['dotnet', ['format', 'backend/DjVisualizer.sln', '--no-restore', '--verify-no-changes']],
  [process.execPath, ['frontend/node_modules/prettier/bin/prettier.cjs', '--check', 'frontend/src', 'frontend/e2e', 'frontend/*.ts', 'frontend/*.json']],
]) {
  const result = spawnSync(command, args, { stdio: 'inherit' });
  if (result.error || result.status !== 0) process.exit(result.status || 1);
}
