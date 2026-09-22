import { test } from 'node:test';
import { throws, doesNotThrow } from 'node:assert/strict';
import { audit } from './audit-nuget.mjs';
test('audit rejects high and critical findings, including transitive packages', () => {
  for (const severity of ['High', 'Critical']) {
    throws(() => audit({ projects: [{ frameworks: [{ transitivePackages: [{ id: 'unsafe', vulnerabilities: [{ severity }] }] }] }] }));
  }
  doesNotThrow(() => audit({ projects: [] }));
  throws(() => audit({ logs: [{ level: 'error' }], projects: [] }));
  throws(() => audit({}));
});
