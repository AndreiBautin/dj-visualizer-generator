import { readFileSync } from 'node:fs';
export function audit(report) {
  if (!report || !Array.isArray(report.projects)) throw new Error('Invalid NuGet audit report');
  if ((report.logs ?? []).some(log => /error/i.test(log.level))) throw new Error('NuGet audit failed');
  for (const project of report.projects) {
    for (const framework of project.frameworks ?? []) {
      for (const pkg of [...(framework.topLevelPackages ?? []), ...(framework.transitivePackages ?? [])]) {
        for (const vulnerability of pkg.vulnerabilities ?? []) {
          if (/^(high|critical)$/i.test(vulnerability.severity)) throw new Error(`Vulnerable package: ${pkg.id}`);
        }
      }
    }
  }
}
if (process.argv[2]) audit(JSON.parse(readFileSync(process.argv[2], 'utf8')));
