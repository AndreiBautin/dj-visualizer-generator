import { readFileSync, readdirSync } from 'node:fs';
const allowed = {
  Domain: [], Application: ['Domain'], Infrastructure: ['Domain', 'Application'],
  'Worker.Hosting': ['Domain', 'Application', 'Infrastructure'],
  Worker: ['Domain', 'Application', 'Infrastructure', 'Worker.Hosting'],
  Api: ['Domain', 'Application', 'Infrastructure', 'Worker.Hosting'],
};
for (const [layer, dependencies] of Object.entries(allowed)) {
  const folder = `backend/src/${layer}`;
  const project = readdirSync(folder).find(x => x.endsWith('.csproj'));
  const xml = readFileSync(`${folder}/${project}`, 'utf8');
  for (const match of xml.replaceAll(String.fromCharCode(92), '/').matchAll(/<ProjectReference Include="\.\.\/([^/]+)\/[^"]*"/g)) {
    if (!dependencies.includes(match[1])) throw new Error(`${layer} must not depend on ${match[1]}`);
  }
  if (layer === 'Domain' && /<PackageReference/.test(xml)) throw new Error('Domain must remain dependency-free');
}
console.log('Architecture boundaries passed.');

function scan(folder) {
  for (const entry of readdirSync(folder, { withFileTypes: true })) {
    if (['bin', 'obj', 'wwwroot'].includes(entry.name)) continue;
    const path = `${folder}/${entry.name}`;
    if (entry.isDirectory()) scan(path);
    else if (entry.name.endsWith('.cs') && /Console\.(Write|Error|Out)/.test(readFileSync(path, 'utf8'))) {
      throw new Error(`Use the configured ILogger sink, not Console: ${path}`);
    }
  }
}
scan('backend/src');
