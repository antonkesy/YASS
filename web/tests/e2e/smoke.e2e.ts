import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { expect, test, type Page } from '@playwright/test';
import { serverFolder } from '../../playwright.config';

// A few paths through the real server (`--fake`, see playwright.config.ts) that unit tests
// cannot cover. Layout, animation and component details belong in the unit tests.

async function scanToReview(page: Page) {
	await page.goto('/');
	await expect(page.getByTestId('scan-button')).toBeEnabled();
	await page.getByTestId('scan-button').click();
	await expect(page).toHaveURL(/\/session\/[A-Za-z0-9-]+$/);
	await expect(page.getByTestId('page-thumb')).toHaveCount(3, { timeout: 20_000 });
}

test('scan, review, edit and download', async ({ page }) => {
	// No showSaveFilePicker, as on Firefox, Safari or a plain-http LAN origin: a plain download.
	await page.addInitScript(() => Reflect.deleteProperty(window, 'showSaveFilePicker'));
	await scanToReview(page);

	const thumbs = page.getByTestId('page-thumb');
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'true');
	await expect(page.getByTestId('page-count')).toHaveText('2 of 3 pages');

	const img = thumbs.nth(0).locator('img');
	await expect(img).toBeVisible();
	const before = await img.getAttribute('src');
	await page.getByRole('button', { name: 'Rotate right' }).first().click();
	await expect(img).not.toHaveAttribute('src', before!);

	await page.getByTestId('name-input').fill('Tax return 2025');
	await page.getByTestId('name-input').press('Enter');
	const download = page.waitForEvent('download');
	await page.getByTestId('download-button').click();
	expect((await download).suggestedFilename()).toMatch(/\.pdf$/);
	await expect(page.getByTestId('session-state')).toHaveText('Saved', { timeout: 20_000 });

	await page.goto('/');
	const tile = page.getByTestId('session-list').getByTestId('session-tile').first();
	await expect(tile).toContainText('Tax return 2025');
	await expect(tile).toContainText('2 p');
});

test('a running scan can be stopped, and keeps its pages', async ({ page }) => {
	await page.goto('/');
	await expect(page.getByTestId('scan-button')).toBeEnabled();
	await page.getByTestId('scan-button').click();
	await expect(page).toHaveURL(/\/session\/[A-Za-z0-9-]+$/);

	await page.getByTestId('stop-scan-button').click();
	await expect(page.getByText('Scan stopped. The pages so far are kept.')).toBeVisible();
	await expect(page.getByTestId('download-button')).toBeVisible();
	expect(await page.getByTestId('page-thumb').count()).toBeLessThan(3);
});

test('a configured server folder can be switched on and saved into', async ({ page }) => {
	// Unique, so a rerun never finds last time's file under the same name.
	const name = `server-save-${Date.now()}`;

	await page.goto('/settings');
	await page.getByTestId('toggle-server-save').locator('input').check();
	await scanToReview(page);
	await page.getByTestId('name-input').fill(name);
	await page.getByTestId('name-input').press('Enter');
	await page.getByTestId('server-save-button').click();

	await expect(page.getByTestId('toast')).toContainText(`Saved to server as ${name}.pdf`);
	const file = join(serverFolder, `${name}.pdf`);
	expect(existsSync(file)).toBe(true);
	expect(readFileSync(file).subarray(0, 4).toString()).toBe('%PDF');
});
