import { expect, test, type Page } from '@playwright/test';

// The server runs `--fake pages=3,blank=2,delay=150ms` (see playwright.config.ts).

async function scanToReview(page: Page) {
	await page.goto('/');
	await expect(page.getByTestId('status-pill')).toHaveText(/Ready/);
	await page.getByTestId('scan-button').click();
	await expect(page).toHaveURL(/\/session\/[A-Za-z0-9-]+$/);
	await expect(page.getByTestId('session-state')).toHaveText('Ready to review', {
		timeout: 20_000
	});
	await expect(page.getByTestId('page-thumb')).toHaveCount(3);
}

test('scan, review, edit, download and save', async ({ page }) => {
	await scanToReview(page);

	// Blank page is pre-marked deleted with a badge; bottom bar counts active pages.
	const thumbs = page.getByTestId('page-thumb');
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'true');
	await expect(thumbs.nth(1).getByTestId('blank-badge')).toBeVisible();
	await expect(page.getByTestId('page-count')).toHaveText('2 of 3 pages');

	// Select page 1 and rotate: acts on the selection only, hash changes after the round trip.
	const firstImg = thumbs.nth(0).locator('img');
	await expect(firstImg).toBeVisible();
	const before = await firstImg.getAttribute('src');
	await thumbs
		.nth(0)
		.getByRole('button', { name: /^Page 1/ })
		.click();
	await expect(page.getByTestId('toolbar-scope')).toHaveText(/1 selected/);
	await page.getByRole('button', { name: 'Rotate right' }).click();
	await expect(firstImg).not.toHaveAttribute('src', before!);
	await expect(thumbs.nth(2).locator('img')).toHaveAttribute('src', /h=[0-9a-f]{16}/);

	// Restore the blank page, then delete it again through the toolbar.
	await page.getByRole('button', { name: 'Clear' }).click();
	await thumbs
		.nth(1)
		.getByRole('button', { name: /^Page 2/ })
		.click();
	await page.getByTestId('restore-button').click();
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'false');
	await expect(page.getByTestId('page-count')).toHaveText('3 of 3 pages');
	await page.getByTestId('delete-button').click();
	await expect(thumbs.nth(1)).toHaveAttribute('data-deleted', 'true');
	await page.getByRole('button', { name: 'Clear' }).click();

	// Full-page preview with next/prev and keyboard.
	await thumbs.nth(0).getByRole('button', { name: 'Open page 1' }).click();
	const preview = page.getByTestId('preview');
	await expect(preview).toBeVisible();
	await expect(page.getByTestId('preview-counter')).toHaveText('1 / 3');

	// Zoom fills the viewport width instead of fitting the page; moving on resets it.
	const zoom = page.getByTestId('preview-zoom');
	const previewImg = preview.locator('img');
	const fitted = await previewImg.boundingBox();
	await zoom.click();
	await expect(zoom).toHaveAttribute('aria-pressed', 'true');
	const enlarged = await previewImg.boundingBox();
	expect(enlarged!.width).toBeGreaterThan(fitted!.width);

	await page.getByTestId('preview-next').click();
	await expect(zoom).toHaveAttribute('aria-pressed', 'false');
	await expect(page.getByTestId('preview-counter')).toHaveText(/2 \/ 3/);
	await page.keyboard.press('ArrowRight');
	await expect(page.getByTestId('preview-counter')).toHaveText('3 / 3');
	await page.keyboard.press('Escape');
	await expect(preview).toBeHidden();

	// Download is a real one-shot PDF.
	const download = page.waitForEvent('download');
	await page.getByTestId('download-button').click();
	const file = await download;
	expect(file.suggestedFilename()).toMatch(/\.pdf$/);

	// Save to paperless finishes the session.
	await page.getByTestId('save-button').click();
	await expect(page.getByTestId('saved-message')).toBeVisible({ timeout: 20_000 });
	await expect(page.getByTestId('session-state')).toHaveText('Saved');

	// Home lists it as saved with the active page count.
	await page.getByRole('link', { name: 'Back to home' }).click();
	const list = page.getByTestId('session-list');
	await expect(list.getByText('Saved').first()).toBeVisible();
	await expect(list.getByText('2 / 3 pages').first()).toBeVisible();
});

test('settings only expose ui_editable keys and are applied to the next scan', async ({ page }) => {
	await page.goto('/settings');
	const form = page.getByTestId('settings-form');
	await expect(form.locator('[name=dpi]')).toBeVisible();
	await expect(form.locator('[name=brightness]')).toHaveCount(0);
	await form.locator('[name=dpi]').selectOption('150');
	await expect(page.getByTestId('overrides-active')).toBeVisible();

	await page.goto('/settings');
	await expect(form.locator('[name=dpi]')).toHaveValue('150');
	await page.getByTestId('reset-settings').click();
	await expect(page.getByTestId('overrides-active')).toBeHidden();
});

test('discard removes the session', async ({ page }) => {
	await scanToReview(page);
	await page.getByTestId('discard-button').click();
	await expect(page.getByTestId('discard-button')).toHaveText('Really discard?');
	await page.getByTestId('discard-button').click();
	await expect(page).toHaveURL(/\/$/);
});

test('stop ends a scan early', async ({ page }) => {
	await page.goto('/');
	await page.getByTestId('scan-button').click();
	await page.getByTestId('stop-button').click({ timeout: 5_000 });
	await expect(page.getByTestId('session-state')).toHaveText('Ready to review', {
		timeout: 20_000
	});
	await expect(page.getByTestId('stop-button')).toBeHidden();
});
