import { test, expect } from '@playwright/test';
import { DemoPage } from '../page-objects/demo.page';

test.describe('Armed mode blocks Playwright', () => {
  // capjs-core's instrumentation script shuffles its detection checks and runs only 8 of them
  // per challenge to deter signature-based bypass. That means a single challenge has roughly
  // a 50% chance of including the navigator.webdriver check that catches Playwright. We retry
  // up to N times to assert that the *system* blocks reliably even if any one challenge doesn't.
  test('instrumentation eventually blocks Playwright within a small number of attempts', async ({ page }) => {
    const maxAttempts = 6;
    let blocked = false;
    let lastStatus = '';
    for (let i = 0; i < maxAttempts && !blocked; i++) {
      const demo = new DemoPage(page, 'armed');
      await demo.goto();
      // Wait either: submit becomes enabled (we passed — keep trying) OR status flips to bad
      // (blocked — done) OR we hit the per-attempt timeout (treat as inconclusive, retry).
      try {
        await Promise.race([
          expect(demo.status).toHaveClass(/bad/, { timeout: 15_000 }).then(() => { blocked = true; }),
          expect(demo.submitBtn).toBeEnabled({ timeout: 15_000 }),
        ]);
      } catch { /* timeout — try again */ }
      lastStatus = (await demo.status.textContent()) ?? '';
      if (blocked) break;
    }
    expect(blocked, `last status after ${maxAttempts} attempts: ${lastStatus}`).toBe(true);
  });

  test('redeem endpoint rejects a stale armed-mode token', async ({ request }) => {
    const r = await request.post('/verify/', { data: { token: 'armed-mode-cannot-produce-a-token' } });
    const body = await r.json();
    expect(body.success).toBe(false);
  });
});
