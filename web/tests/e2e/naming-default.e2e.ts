import { expect, test } from '@playwright/test';

/**
 * What a browser that has never touched the setting sees. Find name is experimental and costs
 * OCR on the server, so it arrives off — and with it off, `{NAME}` is not offered anywhere,
 * because nothing would ever fill it.
 *
 * Its own file rather than a case in `naming.e2e.ts`: that suite seeds the toggle on for every
 * test, which is precisely what this one must not have done for it.
 */

test('Find name is off out of the box, and says it is experimental', async ({ page }) => {
	await page.goto('/settings');
	const section = page.getByTestId('naming-section');
	await expect(section).toBeVisible();

	await expect(section.getByRole('checkbox', { name: /^Find name/ })).not.toBeChecked();
	await expect(section.getByTestId('toggle-badge-find_name')).toHaveText('Experimental');
	await expect(page.getByTestId('find-name-off')).toBeVisible();

	// Nothing that configures a model is on screen until the feature is asked for.
	await expect(section.getByRole('radio', { name: 'OpenAI-style' })).toHaveCount(0);
	await expect(page.getByTestId('ocr-state')).toHaveCount(0);
});

test('{NAME} is not offered while nothing can fill it', async ({ page }) => {
	await page.goto('/settings');
	const section = page.getByTestId('naming-section');

	await expect(section.getByRole('button', { name: '{YEAR}' })).toBeVisible();
	await expect(section.getByRole('button', { name: '{NAME}' })).toHaveCount(0);

	await page.getByTestId('template-presets').click();
	const menu = page.getByRole('menu');
	await expect(menu.getByRole('menuitem')).toHaveCount(3);
	await expect(menu.getByRole('menuitem').filter({ hasText: '{NAME}' })).toHaveCount(0);
});

test('a scan is named by the template alone, with nothing to click', async ({ page }) => {
	await page.goto('/settings');
	await page.getByTestId('template-input').fill('{YEAR}-{MONTH}-{DAY}');

	await page.goto('/');
	await page.getByTestId('scan-button').click();
	await expect(page).toHaveURL(/\/session\/[A-Za-z0-9-]+$/);
	await expect(page.getByTestId('session-state')).toHaveText('Ready to review', {
		timeout: 20_000
	});

	// No sparkle, no slot: the name field is a field, and the name is the template.
	await expect(page.getByTestId('name-suggest')).toHaveCount(0);
	await expect(page.getByTestId('name-generated')).toHaveCount(0);
	await expect(page.getByTestId('name-input')).toHaveValue(/^\d{4}-\d{2}-\d{2}$/);
});
