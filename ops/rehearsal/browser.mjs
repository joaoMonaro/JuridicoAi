// Browser is only a test client. Both application services run from clean images.
import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const state = process.argv[2];
const readOnly = process.argv.includes('--read-only');
const cfg = JSON.parse(await readFile(path.join(state, 'state.json'), 'utf8'));
const expected = JSON.parse(await readFile(path.join(state, 'expected.json'), 'utf8'));
const require = createRequire(path.join(cfg.frontend, 'package.json'));
const { chromium, expect } = require('@playwright/test');
const origin = `https://app.juridicoai.test:${cfg.HTTPS_PORT}`;
const api = `https://api.juridicoai.test:${cfg.HTTPS_PORT}`;
const browser = await chromium.launch({args: ['--host-resolver-rules=MAP *.juridicoai.test 127.0.0.1', '--no-proxy-server']});
const context = await browser.newContext({ignoreHTTPSErrors: true, acceptDownloads: true});
const page = await context.newPage();
const failures = [];
page.on('pageerror', error => failures.push(error.message));
page.on('request', request => {
  const url = new URL(request.url());
  if (url.protocol === 'https:' && !['app.juridicoai.test', 'api.juridicoai.test'].includes(url.hostname))
    failures.push('Unexpected external browser request: ' + url.hostname);
});
try {
  await page.goto(origin + '/login');
  await page.getByRole('textbox', {name: 'E-mail', exact: true}).fill('pilot@example.test');
  await page.getByLabel('Senha', {exact: true}).fill(cfg.PILOT_PASSWORD);
  await page.getByRole('button', {name: 'Entrar', exact: true}).click();
  await expect(page.getByText('Ensaio local fictício', {exact: true})).toBeVisible();
  await page.getByRole('listitem').filter({hasText: 'Ensaio local fictício'}).click();
  const editor = page.getByRole('textbox', {name: 'Conteúdo da peça'});
  await expect(editor).toContainText('Minuta fictícia: a Empresa Exemplo');
  if (!readOnly) {
    const refreshed = page.waitForResponse(response => response.url().endsWith('/auth/refresh') && response.request().method() === 'POST');
    await page.evaluate(() => {
      const key = 'juridicoai.auth-session';
      const session = JSON.parse(sessionStorage.getItem(key));
      session.expiresAt = Date.now() - 1000;
      sessionStorage.setItem(key, JSON.stringify(session));
    });
    await editor.click();
    await page.keyboard.press('Control+End');
    await page.keyboard.insertText(' Complemento manual do ensaio.');
    await page.getByRole('button', {name: 'Salvar', exact: true}).click();
    if ((await refreshed).status() !== 200) throw new Error('Real cookie refresh failed');
    await expect(page.getByText('Salvo', {exact: true})).toBeVisible();
  }
  const session = await page.evaluate(() => JSON.parse(sessionStorage.getItem('juridicoai.auth-session')));
  const piece = await page.evaluate(async ({api, id, token}) => {
    const response = await fetch(`${api}/pecas/${id}`, {headers: {Authorization: `Bearer ${token}`}, credentials: 'include'});
    if (!response.ok) throw new Error('Cannot read saved piece');
    return response.json();
  }, {api, id: expected.pieceId, token: session.accessToken});
  if (!readOnly) {
    expected.accessToken = session.accessToken;
    expected.version = piece.versao;
    expected.document = piece.conteudoJson;
    await writeFile(path.join(state, 'expected.json'), JSON.stringify(expected, null, 2), {mode: 0o600});
  } else {
    if (piece.versao !== expected.version || JSON.stringify(piece.conteudoJson) !== JSON.stringify(expected.document))
      throw new Error('Restored piece differs');
  }
  await page.reload();
  await expect(editor).toContainText('Complemento manual do ensaio.');
  await page.getByRole('button', {name: 'Abrir assistente de IA'}).click();
  await expect(page.getByRole('textbox', {name: 'O que você quer que a IA faça?'})).toBeEnabled();
  await expect(page.getByText('Reescreva o primeiro parágrafo para conferir uma proposta fictícia.', {exact: true})).toBeVisible();
  const cookies = await context.cookies(api + '/auth/refresh');
  const cookie = cookies.find(value => value.name === 'juridicoai_refresh');
  if (!cookie?.secure || !cookie.httpOnly || cookie.path !== '/auth') throw new Error('Refresh cookie protections missing');
  for (const [format, label] of [['docx', 'Word (.docx)'], ['pdf', 'PDF (.pdf)']]) {
    await page.getByRole('button', {name: 'Exportar', exact: true}).click();
    const downloading = page.waitForEvent('download');
    await page.getByRole('menuitem', {name: label, exact: true}).click();
    const download = await downloading;
    await download.saveAs(path.join(state, `browser-${readOnly ? 'restored' : 'original'}.${format}`));
    if (await download.failure()) throw new Error('Browser download failed');
  }
  await page.screenshot({path: path.join(state, `browser-${readOnly ? 'restored' : 'original'}.png`), fullPage: true});
  if (failures.length) throw new Error(failures.join('\n'));
  console.log(JSON.stringify({status:'passed', realBackend:true, productionBuild:true, https:true,
    realCookieRefresh: !readOnly, reload:true, chatRecovered:true, downloads:['docx','pdf'], restored:readOnly, pageErrors:0}));
} catch (error) {
  await page.screenshot({path: path.join(state, `browser-${readOnly ? 'restored' : 'original'}-failure.png`), fullPage: true}).catch(() => {});
  throw error;
} finally { await browser.close(); }
