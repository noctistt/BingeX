// Research harness only. Node is not added to BingeX or its installer.
import fs from 'node:fs/promises';
import path from 'node:path';
import vm from 'node:vm';
import { stripTypeScriptTypes } from 'node:module';
import { spawn } from 'node:child_process';

const [sourceDir, dotnet, nativeProbe, output] = process.argv.slice(2);
if (!output) throw new Error('Arguments: reviewed plugin src directory, dotnet, native probe DLL, report path');
const root = path.resolve(sourceDir);
const providers = new Map(), modules = new Map();
const findings = { scope: 'Unmodified reviewed Nuclear SoundCloud plugin; temporary SDK interfaces; no accounts, saved streams, or BingeX profile changes', requests: 0, stages: [] };
let stage = 'module-load';
const lifetime = AbortSignal.timeout(150_000);
const allowed = host => host === 'soundcloud.com' || host.endsWith('.soundcloud.com') || host === 'sndcdn.com' || host.endsWith('.sndcdn.com') || host === 'playback.media-streaming.soundcloud.cloud';
function reason(error) {
  const message = String(error.message ?? '');
  if (message.startsWith('No HLS transcoding found')) return 'no-hls-transcoding';
  if (message.startsWith('Failed to resolve stream URL:')) return 'stream-http-' + message.match(/\d+/)?.[0];
  if (message.startsWith('SoundCloud API error:')) return 'api-http-' + message.match(/\d+/)?.[0];
  if (message === 'Media boundary') return 'media-host-outside-research-allowlist';
  return error.name;
}
const api = {
  Providers: { register(provider) { providers.set(provider.kind, provider); }, unregister() {} },
  Logger: { debug() {}, info() {}, warn() {}, error() {} },
  Http: { async fetch(input, init = {}) {
    let url = new URL(input);
    for (let hop = 0; hop < 4; hop++) {
      if (++findings.requests > 50 || url.protocol !== 'https:' || url.username || url.password || !allowed(url.hostname) || (init.method && init.method !== 'GET')) throw new Error('Research request boundary');
      const response = await fetch(url, { ...init, redirect: 'manual', signal: AbortSignal.any([lifetime, AbortSignal.timeout(12_000)]) });
      if (response.status >= 300 && response.status < 400 && response.headers.has('location')) { url = new URL(response.headers.get('location'), url); await response.body?.cancel(); continue; }
      if (!response.ok) findings.stages.push({ stage: 'http', status: response.status });
      const reader = response.body?.getReader(); let size = 0; const parts = [];
      if (reader) { while (true) { const { done, value } = await reader.read(); if (done) break; size += value.length; if (size > 8_000_000) { await reader.cancel(); throw new Error('Research response limit'); } parts.push(value); } }
      const body = Buffer.concat(parts);
      if (response.ok && /^\/tracks\/\d+$/.test(url.pathname)) {
        const track = JSON.parse(body.toString());
        findings.stages.push({ stage: 'track-formats', policy: track.policy, streamable: track.streamable, formats: (track.media?.transcodings ?? []).map(x => ({ protocol: x.format?.protocol, mimeType: x.format?.mime_type })) });
      }
      return new Response(body, { status: response.status, headers: response.headers });
    }
    throw new Error('Research redirect limit');
  } },
};
const context = vm.createContext({ URL, URLSearchParams, console: { log() {}, warn() {}, error() {}, debug() {} } });
async function load(filename) {
  filename = path.resolve(filename);
  if (!filename.startsWith(root + path.sep)) throw new Error('Module boundary');
  if (modules.has(filename)) return modules.get(filename);
  const code = stripTypeScriptTypes(await fs.readFile(filename, 'utf8'));
  const module = new vm.SourceTextModule(code, { context, identifier: filename }); modules.set(filename, module);
  return module;
}
function native(sources) {
  return new Promise((resolve, reject) => {
    const child = spawn(dotnet, [nativeProbe], { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true });
    let stdout = ''; const timeout = setTimeout(() => { child.kill(); reject(new Error('Native deadline')); }, 120_000);
    child.stdout.on('data', chunk => { stdout += chunk; if (stdout.length > 20_000) child.kill(); });
    child.stderr.on('data', () => {}); // Native diagnostics can contain signed URLs.
    child.once('error', error => { clearTimeout(timeout); reject(error); });
    child.once('close', code => { clearTimeout(timeout); try { const result = JSON.parse(stdout.trim()); resolve({ exitCode: code, ...result }); } catch { reject(new Error('Native response')); } });
    child.stdin.end(JSON.stringify(sources.map(s => ({ url: s.url, headers: null }))));
  });
}
try {
  const plugin = await load(path.join(root, 'index.ts'));
  await plugin.link((specifier, parent) => { if (!specifier.startsWith('./')) throw new Error('Unexpected runtime dependency'); return load(path.join(path.dirname(parent.identifier), specifier + '.ts')); });
  await plugin.evaluate({ timeout: 3000 });
  plugin.namespace.default.onEnable(api);
  findings.stages.push({ stage, result: 'pass', registered: [...providers.keys()] });
  const streaming = providers.get('streaming'), metadata = providers.get('metadata');
  const resolved = [];
  for (const query of ['Kevin MacLeod', 'Arijit Singh']) {
    stage = 'search';
    try {
      const results = await metadata.searchTracks({ query, limit: 5 });
      findings.stages.push({ stage, query, result: 'pass', count: results.length, artwork: results.some(x => x.artwork) });
      stage = 'stream-search';
      const candidates = await streaming.searchForTrack(query, '');
      findings.stages.push({ stage, query, result: 'pass', count: candidates.length });
      for (const candidate of candidates.slice(0, 3)) {
        if (resolved.length >= 2) break;
        stage = 'resolve';
        try {
          const stream = await streaming.getStreamUrl(candidate.id ?? candidate.source?.id);
          const url = new URL(stream.url);
          findings.stages.push({ stage: 'media-host', host: url.hostname, scheme: url.protocol });
          if (url.protocol !== 'https:' || !allowed(url.hostname)) throw new Error('Media boundary');
          if (stream.durationMs < 30_000) continue;
          resolved.push(stream);
          findings.stages.push({ stage, query, result: 'pass', protocol: stream.protocol, mimeType: stream.mimeType, estimatedBitrateKbps: stream.bitrateKbps });
          break; // One source from each search for the two-track native test.
        } catch (error) { findings.stages.push({ stage, result: 'failed', error: reason(error) }); }
      }
    } catch (error) { findings.stages.push({ stage, query, result: 'failed', error: error.name }); }
  }
  if (resolved.length === 2) { stage = 'native-playback'; findings.stages.push({ stage, ...await native(resolved) }); }
  else findings.stages.push({ stage: 'native-playback', result: 'not-tested', reason: 'Fewer than two resolved sources' });
} catch (error) { findings.stages.push({ stage, result: 'failed', error: error.name }); }
await fs.writeFile(output, JSON.stringify(findings, null, 2) + '\n');
console.log(JSON.stringify(findings, null, 2));
process.exitCode = findings.stages.some(s => s.stage === 'native-playback' && s.result === 'pass') ? 0 : 1;
