import { test, expect } from '@playwright/test';
import { DemoPage } from '../page-objects/demo.page';

test.describe('CapNet happy path (test mode: instrumentation non-blocking)', () => {
  test('widget solves challenge and server accepts redeem token', async ({ page }) => {
    const demo = new DemoPage(page, 'test');
    await demo.goto();
    await demo.waitForSolved();
    await demo.submit();
    await expect(demo.status).toHaveText(/Success: redeem token accepted/);
  });

  test('server rejects unknown redeem token', async ({ request }) => {
    const r = await request.post('/verify/', { data: { token: 'not-a-real-token' } });
    expect(r.ok()).toBeTruthy();
    const body = await r.json();
    expect(body.success).toBe(false);
  });

  test('server rejects replay of consumed token', async ({ page, request }) => {
    const demo = new DemoPage(page, 'test');
    await demo.goto();
    await demo.waitForSolved();
    const token = await demo.readToken();
    expect(token, 'widget should expose the redeem token').toBeTruthy();
    await demo.submit();
    await expect(demo.status).toHaveText(/Success/);

    const r = await request.post('/verify/', { data: { token } });
    const body = await r.json();
    expect(body.success).toBe(false);
  });

  test('challenge endpoint returns format-1 shape with top-level instrumentation', async ({ request }) => {
    const r = await request.post('/cap-test/challenge');
    expect(r.ok()).toBeTruthy();
    const body = await r.json();
    expect(body).toHaveProperty('token');
    expect(body).toHaveProperty('expires');
    expect(body.challenge).toMatchObject({ c: expect.any(Number), s: expect.any(Number), d: expect.any(Number) });
    expect(body).toHaveProperty('instrumentation');
  });
});
