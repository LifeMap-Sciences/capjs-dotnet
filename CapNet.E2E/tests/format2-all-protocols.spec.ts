import { test, expect, Page } from '@playwright/test';

/**
 * Drives the format-2 envelope (sha256-pow + RSW + instrumentation) end-to-end via a custom
 * in-page solver. The upstream @cap.js/widget v0.1.51 has a state-machine race for format-2
 * speculative fetches, so we hand-roll the solve to verify the .NET library forwards every
 * option correctly to capjs-core and the validation path accepts all three protocols.
 */
test.describe('Format 2 envelope (PoW + RSW + instrumentation)', () => {
  test('server issues format-2 challenge with all three protocols and accepts a valid solve', async ({ page }) => {
    await page.goto('/?bypass=1');

    const result = await page.evaluate(async () => {
      // ── Helpers ──────────────────────────────────────────────────────────
      function fnv1a(s: string): number {
        let h = 2166136261 >>> 0;
        for (let i = 0; i < s.length; i++) {
          h ^= s.charCodeAt(i);
          h = (h + (h << 1) + (h << 4) + (h << 7) + (h << 8) + (h << 24)) >>> 0;
        }
        return h >>> 0;
      }
      async function sha256Hex(data: string): Promise<Uint8Array> {
        const buf = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(data));
        return new Uint8Array(buf);
      }
      function matchesHexPrefix(bytes: Uint8Array, hexTarget: string): boolean {
        const len = hexTarget.length;
        const fullBytes = len >> 1;
        for (let i = 0; i < fullBytes; i++) {
          const expected = parseInt(hexTarget.substring(i * 2, i * 2 + 2), 16);
          if (bytes[i] !== expected) return false;
        }
        if (len & 1) {
          const partial = parseInt(hexTarget[len - 1], 16);
          if ((bytes[fullBytes] >> 4) !== partial) return false;
        }
        return true;
      }
      async function solveSha256Pow(salt: string, target: string): Promise<number> {
        for (let nonce = 0; nonce < 10_000_000; nonce++) {
          const h = await sha256Hex(salt + nonce);
          if (matchesHexPrefix(h, target)) return nonce;
        }
        throw new Error('PoW solver exceeded budget');
      }
      function solveRsw(N: string, x: string, t: number): string {
        const n = BigInt('0x' + N);
        let y = BigInt('0x' + x);
        for (let i = 0; i < t; i++) y = (y * y) % n;
        let hex = y.toString(16);
        while (hex.length < N.length) hex = '0' + hex;
        return hex;
      }

      // ── 1) Issue challenge ───────────────────────────────────────────────
      const chResp = await fetch('/cap-v2/challenge', { method: 'POST' });
      const ch = await chResp.json();
      const summary = {
        format: ch.format,
        token: !!ch.token,
        challengeCount: ch.challenges?.length,
        protocols: ch.challenges ? [...new Set(ch.challenges.map((c: any) => c.protocol))] : [],
      };

      // ── 2) Solve every challenge ─────────────────────────────────────────
      const solutions: any[] = [];
      for (const c of ch.challenges) {
        if (c.protocol === 'sha256-pow') {
          const nonce = await solveSha256Pow(c.payload.salt, c.payload.target);
          solutions.push({ nonce });
        } else if (c.protocol === 'rsw') {
          const y = solveRsw(c.payload.N, c.payload.x, c.payload.t);
          solutions.push({ y });
        } else if (c.protocol === 'instrumentation') {
          // Non-blocking config: capjs-core lets `blocked: true` fall through when
          // blockAutomatedBrowsers is false. (`timeout: true` is always rejected.)
          solutions.push({ blocked: true });
        }
      }

      // ── 3) Redeem ────────────────────────────────────────────────────────
      const rdResp = await fetch('/cap-v2/redeem', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: ch.token, solutions }),
      });
      const rd = await rdResp.json();
      return { summary, redeem: { status: rdResp.status, body: rd } };
    });

    expect(result.summary.format).toBe(2);
    expect(result.summary.token).toBe(true);
    expect(result.summary.protocols.sort()).toEqual(['instrumentation', 'rsw', 'sha256-pow']);
    expect(result.summary.challengeCount).toBeGreaterThan(0);

    // Redeem should succeed: PoW solved, RSW squared, instrumentation timeout accepted (non-blocking).
    expect(result.redeem.status).toBe(200);
    expect(result.redeem.body.success).toBe(true);
    expect(result.redeem.body.token).toBeTruthy();
  });

  test('server rejects bad PoW solutions in format 2', async ({ page }) => {
    await page.goto('/?bypass=1');

    const result = await page.evaluate(async () => {
      const chResp = await fetch('/cap-v2/challenge', { method: 'POST' });
      const ch = await chResp.json();
      // Submit garbage for every entry.
      const solutions = ch.challenges.map((c: any) => {
        if (c.protocol === 'sha256-pow') return { nonce: 0 };
        if (c.protocol === 'rsw') return { y: '00' };
        return { timeout: true };
      });
      const r = await fetch('/cap-v2/redeem', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: ch.token, solutions }),
      });
      return { status: r.status, body: await r.json() };
    });

    expect(result.status).toBe(403);
    expect(result.body.success).toBe(false);
  });
});
