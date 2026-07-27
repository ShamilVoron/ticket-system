const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE = 'http://localhost:3000';
const OUT = path.join(__dirname, 'ui-audit');
const findings = [];

function note(severity, area, message) {
  findings.push({ severity, area, message });
  console.log(`[${severity}] ${area}: ${message}`);
}

async function safeClick(page, selector, label) {
  try {
    const el = page.locator(selector).first();
    if (await el.count() && (await el.isVisible())) {
      await el.click({ timeout: 5000 });
      await page.waitForTimeout(800);
      return true;
    }
  } catch (e) {
    note('warn', label || selector, `click failed: ${e.message}`);
  }
  return false;
}

async function shot(page, name) {
  await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage: true });
}

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  const consoleErrors = [];
  const pageErrors = [];
  const failedRequests = [];

  page.on('console', (msg) => {
    if (msg.type() === 'error') consoleErrors.push(`${page.url()} :: ${msg.text()}`);
  });
  page.on('pageerror', (err) => pageErrors.push(`${page.url()} :: ${err.message}`));
  page.on('response', (res) => {
    if (res.status() >= 400 && !res.url().includes('_nuxt')) {
      failedRequests.push(`${res.status()} ${res.request().method()} ${res.url()}`);
    }
  });

  // Login
  await page.goto(`${BASE}/auth/login`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1500);
  await shot(page, '01-login');
  await page.fill('input[type="password"], input[name="password"]', 'admin123');
  // username field - try common selectors
  const userInput = page.locator('input[type="text"], input[type="email"], input[name="username"], input[name="email"]').first();
  await userInput.fill('admin@local.dev');
  await page.click('button[type="submit"]');
  await page.waitForTimeout(2500);
  // Avoid onboarding redirect loop during audit
  await page.evaluate(() => {
    localStorage.setItem('onboarding_skip', '1');
  });
  await shot(page, '02-after-login');

  const url = page.url();
  if (url.includes('/auth/login')) {
    note('critical', 'auth', 'Login failed — still on login page');
  } else {
    note('ok', 'auth', `Logged in, landed on ${url}`);
  }

  // Onboarding if present
  if (url.includes('onboarding') || (await page.locator('text=Настройка').count())) {
    note('info', 'onboarding', 'Onboarding wizard shown after login (expected for first run)');
    // Skip wizard to reach main app
    const skip = page.getByRole('button', { name: /Пропустить|Skip/i });
    if (await skip.count()) {
      await skip.click();
      await page.waitForTimeout(500);
      // may need skip multiple steps
      for (let i = 0; i < 3; i++) {
        const s = page.getByRole('button', { name: /Пропустить|Skip|Готово|Завершить/i });
        if (await s.count()) {
          await s.first().click();
          await page.waitForTimeout(600);
        }
      }
    }
    await shot(page, '03-onboarding');
  }

  // Profile label redundancy check
  const headerBtn = page.locator('header button').filter({ hasText: /Super Admin|Пользователь/i }).first();
  const headerText = (await headerBtn.innerText().catch(() => '')) || '';
  if (/Super Admin/i.test(headerText) && /Супер\s*Админ/i.test(headerText)) {
    note('medium', 'profile', 'Дублирование роли в шапке: EN + RU');
  } else {
    note('ok', 'profile', `Шапка профиля без дубля роли: "${headerText.replace(/\s+/g, ' ').trim()}"`);
  }

  // Theme: dark then light
  await page.evaluate(() => {
    document.documentElement.classList.add('dark');
    localStorage.setItem('ticket-system-theme', 'dark');
  });
  await page.reload({ waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1500);
  // Skip onboarding again if redirected
  for (let i = 0; i < 4; i++) {
    const s = page.getByRole('button', { name: /Пропустить|Готово|Завершить/i });
    if (await s.count() && (await s.first().isVisible())) {
      await s.first().click();
      await page.waitForTimeout(500);
    } else break;
  }
  await shot(page, '04-theme-dark');

  // Try toggle theme via UI
  const themeBtn = page.locator('button').filter({ hasText: /тем|свет|theme|Theme/i });
  if (!(await themeBtn.count())) {
    // open profile dropdown
    await safeClick(page, 'text=Super Admin', 'profile-menu');
    await page.waitForTimeout(400);
    await shot(page, '05-profile-menu');
  }

  // Force light theme
  await page.evaluate(() => {
    document.documentElement.classList.remove('dark');
    localStorage.setItem('ticket-system-theme', 'light');
  });
  await page.reload({ waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1500);
  for (let i = 0; i < 4; i++) {
    const s = page.getByRole('button', { name: /Пропустить|Готово|Завершить/i });
    if (await s.count() && (await s.first().isVisible())) {
      await s.first().click();
      await page.waitForTimeout(500);
    } else break;
  }
  await shot(page, '06-theme-light');

  // Light sidebar must not stay near-black
  const lightSidebar = await page.evaluate(() => {
    const side = document.querySelector('aside');
    if (!side) return null;
    const cs = getComputedStyle(side);
    const m = cs.backgroundColor.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/);
    if (!m) return { bg: cs.backgroundColor, luminance: null };
    const [r, g, b] = [Number(m[1]), Number(m[2]), Number(m[3])];
    const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
    return { bg: cs.backgroundColor, luminance };
  });
  if (lightSidebar?.luminance != null && lightSidebar.luminance < 0.35) {
    note('high', 'theme', `Светлая тема: sidebar всё ещё тёмный (${lightSidebar.bg})`);
  } else if (lightSidebar) {
    note('ok', 'theme', `Светлый sidebar: ${lightSidebar.bg}`);
  }

  const htmlClass = await page.evaluate(() => document.documentElement.className);
  note('info', 'theme', `html class after light: "${htmlClass}"`);

  // Sidebar groups collapsed by default (before visiting /companies)
  const clientsOpenEarly = await page.evaluate(() => {
    const links = [...document.querySelectorAll('aside a')].filter((a) => {
      const t = (a.textContent || '').trim();
      return t === 'Юрлица' || t === 'Реестр';
    });
    return links.some((a) => {
      const r = a.getBoundingClientRect();
      return r.height > 0 && r.width > 0;
    });
  });
  if (clientsOpenEarly) note('medium', 'sidebar', 'Клиенты/Оборудование видны развёрнутыми на главной');
  else note('ok', 'sidebar', 'Подменю Клиенты/Оборудование свёрнуты');

  // Empty state copy on tickets
  const ticketsBodyEarly = await page.locator('body').innerText();
  if (/Пока нет заявок/i.test(ticketsBodyEarly)) {
    note('ok', 'empty', 'Пустой список без фильтров: корректный текст');
  } else if (/Попробуйте изменить параметры поиска/i.test(ticketsBodyEarly)) {
    note('medium', 'empty', 'Пустой список всё ещё предлагает менять фильтры');
  }

  // Navigate pages
  const routes = [
    ['/', 'all-tickets'],
    ['/my', 'my-tickets'],
    ['/tickets/new', 'new-ticket'],
    ['/messenger', 'messenger'],
    ['/companies', 'companies'],
    ['/objects', 'objects'],
    ['/equipment', 'equipment'],
    ['/employees', 'employees'],
    ['/schedule', 'schedule'],
    ['/reports', 'reports'],
    ['/spreadsheets', 'spreadsheets'],
    ['/settings', 'settings'],
    ['/profile', 'profile'],
    ['/sync', 'sync'],
    ['/onboarding', 'onboarding-page'],
  ];

  for (const [route, name] of routes) {
    const beforeFails = failedRequests.length;
    await page.goto(`${BASE}${route}`, { waitUntil: 'domcontentloaded', timeout: 30000 }).catch((e) => {
      note('high', route, `navigation error: ${e.message}`);
    });
    await page.waitForTimeout(1800);
    await shot(page, `page-${name}`);

    // empty / error UI
    const body = await page.locator('body').innerText().catch(() => '');
    if (/404|Not Found|страница не найдена/i.test(body)) {
      note('high', route, 'Страница показывает 404 / not found');
    }
    if (/Cannot read|undefined is not|TypeError|Unhandled/i.test(body)) {
      note('critical', route, 'Видимая JS/runtime ошибка на странице');
    }
    // API 404s for this navigation
    const newFails = failedRequests.slice(beforeFails).filter((f) => f.startsWith('404') || f.startsWith('500'));
    if (newFails.length) {
      note('high', route, `API errors: ${[...new Set(newFails)].slice(0, 5).join(' | ')}`);
    }

    // Light theme specific: check if sidebar is unreadable
    if (name === 'settings' || name === 'all-tickets') {
      const styles = await page.evaluate(() => {
        const side = document.querySelector('aside, nav, [class*="sidebar"]');
        if (!side) return null;
        const cs = getComputedStyle(side);
        return { bg: cs.backgroundColor, color: cs.color };
      });
      if (styles) note('info', `${route}-sidebar`, `bg=${styles.bg} color=${styles.color}`);
    }
  }

  // Settings branding color picker mismatch
  await page.goto(`${BASE}/settings`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1800);
  // try open general / branding tab
  await safeClick(page, 'text=Общие', 'settings-general');
  await safeClick(page, 'text=Брендинг', 'settings-branding');
  await shot(page, '07-settings-branding');

  const colorInput = page.locator('input[type="color"]').first();
  if (await colorInput.count()) {
    const val = await colorInput.inputValue().catch(() => '');
    if (val && val.toLowerCase() !== '#000000') {
      note('ok', 'branding', `Accent color picker: ${val}`);
    } else {
      note('medium', 'branding', `Accent color picker looks black/empty: ${val}`);
    }
  }

  // Status color labels should not say Custom
  await safeClick(page, 'text=Статусы', 'settings-statuses');
  await page.waitForTimeout(600);
  const statusBody = await page.locator('body').innerText();
  if (/\bCustom\b/.test(statusBody)) {
    note('medium', 'settings', 'В таблице статусов всё ещё есть метка Custom');
  } else {
    note('ok', 'settings', 'Метка Custom в статусах не найдена');
  }

  // Onboarding without staff sidebar
  await page.goto(`${BASE}/onboarding`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1200);
  await shot(page, '12-onboarding-layout');
  const onboardingHasNav = await page.evaluate(() => {
    const aside = document.querySelector('aside');
    if (!aside) return false;
    const text = aside.innerText || '';
    return /Все заявки|Мессенджер|Настройки/.test(text);
  });
  if (onboardingHasNav) {
    note('medium', 'onboarding-ux', 'Онбординг всё ещё внутри staff sidebar');
  } else {
    note('ok', 'onboarding-ux', 'Онбординг без staff-навигации');
  }

  // SignalR: expect direct backend WS in dev (no WS 200 via :3000)
  const wsErrors = consoleErrors.filter((e) => /WebSocket|signalr|Failed to start/i.test(e));
  if (wsErrors.length) {
    note('medium', 'signalr', wsErrors.slice(0, 3).join(' | '));
  } else {
    note('ok', 'signalr', 'Нет явных ошибок SignalR/WebSocket в console');
  }

  // New ticket form suggest
  await page.goto(`${BASE}/tickets/new`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1800);
  await shot(page, '08-new-ticket');
  const titleSelectors = [
    'input[placeholder*="Тема"]',
    'input[placeholder*="тему"]',
    'input[placeholder*="назван"]',
    'textarea',
  ];
  for (const sel of titleSelectors) {
    if (await page.locator(sel).count()) {
      await page.locator(sel).first().fill('Срочный ремонт принтера');
      await page.locator(sel).first().blur();
      break;
    }
  }
  await page.waitForTimeout(800);
  await shot(page, '09-new-ticket-filled');

  // Messenger
  await page.goto(`${BASE}/messenger`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2000);
  await shot(page, '10-messenger');

  // Employees
  await page.goto(`${BASE}/employees`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1800);
  await shot(page, '11-employees');

  // Deduplicate failed requests
  const uniqFails = [...new Set(failedRequests)];
  for (const f of uniqFails.slice(0, 40)) {
    if (f.startsWith('404') || f.startsWith('500') || f.startsWith('503')) {
      note(f.startsWith('500') ? 'critical' : 'high', 'api', f);
    }
  }
  for (const e of [...new Set(consoleErrors)].slice(0, 20)) {
    note('medium', 'console', e);
  }
  for (const e of [...new Set(pageErrors)].slice(0, 10)) {
    note('critical', 'pageerror', e);
  }

  fs.writeFileSync(path.join(OUT, 'findings.json'), JSON.stringify(findings, null, 2), 'utf8');
  console.log('\n=== SUMMARY ===');
  console.log(`findings: ${findings.length}`);
  console.log(`screenshots: ${fs.readdirSync(OUT).filter((f) => f.endsWith('.png')).length}`);
  const by = findings.reduce((acc, f) => {
    acc[f.severity] = (acc[f.severity] || 0) + 1;
    return acc;
  }, {});
  console.log('by severity:', by);
  await browser.close();
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
