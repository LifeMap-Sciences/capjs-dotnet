import { Page, Locator, expect } from '@playwright/test';

export class DemoPage {
  readonly page: Page;
  readonly widget: Locator;
  readonly submitBtn: Locator;
  readonly status: Locator;
  readonly mode: 'armed' | 'test';

  constructor(page: Page, mode: 'armed' | 'test' = 'test') {
    this.page = page;
    this.mode = mode;
    this.widget = page.locator('#cap');
    this.submitBtn = page.locator('#submit');
    this.status = page.getByTestId('status');
  }

  async goto() {
    await this.page.goto(this.mode === 'test' ? '/?bypass=1' : '/');
  }

  async waitForSolved(timeoutMs = 45_000) {
    await expect(this.submitBtn).toBeEnabled({ timeout: timeoutMs });
  }

  async expectBlocked(timeoutMs = 45_000) {
    await expect(this.status).toHaveClass(/bad/, { timeout: timeoutMs });
  }

  async submit() {
    await this.submitBtn.click();
  }

  async readToken(): Promise<string | null> {
    return await this.page.evaluate(() => (document.getElementById('cap') as any)?.token ?? null);
  }
}
