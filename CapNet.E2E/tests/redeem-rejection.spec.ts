import { test, expect } from '@playwright/test';

test.describe('CapNet redeem rejection paths', () => {
  test('rejects malformed JWT', async ({ request }) => {
    const r = await request.post('/cap-test/redeem', {
      data: { token: 'not.a.jwt', solutions: [0] },
    });
    expect(r.status()).toBe(403);
    const body = await r.json();
    expect(body.success).toBe(false);
    expect(body.reason).toBe('invalid_token');
  });

  test('rejects missing solutions', async ({ request }) => {
    const ch = await request.post('/cap-test/challenge');
    const issued = await ch.json();
    const r = await request.post('/cap-test/redeem', { data: { token: issued.token } });
    expect(r.status()).toBe(403);
    const body = await r.json();
    expect(body.reason).toBe('missing_solutions');
  });

  test('rejects wrong solution count', async ({ request }) => {
    const ch = await request.post('/cap-test/challenge');
    const issued = await ch.json();
    const r = await request.post('/cap-test/redeem', {
      data: { token: issued.token, solutions: [1] },
    });
    expect(r.status()).toBe(403);
    const body = await r.json();
    expect(body.reason).toBe('invalid_solutions');
  });

  test('rejects all-zero solutions', async ({ request }) => {
    const ch = await request.post('/cap-test/challenge');
    const issued = await ch.json();
    const zeros = Array.from({ length: issued.challenge.c }, () => 0);
    const r = await request.post('/cap-test/redeem', {
      data: { token: issued.token, solutions: zeros },
    });
    expect(r.status()).toBe(403);
    const body = await r.json();
    expect(body.reason).toBe('invalid_solution');
  });
});
