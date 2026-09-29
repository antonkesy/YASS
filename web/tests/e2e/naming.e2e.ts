import { expect, test, type Page } from '@playwright/test';

// The server runs with the default `naming:` block: no OCR, no provider (see playwright.config.ts).

/** Answers the model host, so the real request path runs without a real provider. */
async function stubModel(page: Page, body: object, status = 200) {
	await page.route('**/chat/completions', (route) =>
		route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
	);
}

async function openNaming(page: Page) {
	await page.goto('/settings');
	const section = page.getByTestId('naming-section');
	await expect(section).toBeVisible();
	return section;
}

test('the settings sheet says what the server can read', async ({ page }) => {
	await openNaming(page);

	// The default server reads nothing, and says so instead of leaving Find name unexplained.
	await expect(page.getByTestId('ocr-state')).toContainText('does not read page text');
	await expect(page.getByTestId('ai-key')).toHaveCount(0);
});

test('a model set up here answers through the real request path', async ({ page }) => {
	// Quotes and an extension, as models like to: what lands on screen must be neither.
	await stubModel(page, { choices: [{ message: { content: '"Quarterly Report.pdf"' } }] });
	const section = await openNaming(page);

	await section.getByRole('radio', { name: 'OpenAI-style' }).check();
	await page.getByTestId('ai-key').fill('sk-not-a-real-key');
	await expect(page.getByTestId('ai-key')).toHaveAttribute('type', 'password');
	await page.getByTestId('ai-test').click();

	await expect(page.getByTestId('ai-test-result')).toContainText('Quarterly Report');
	await expect(page.getByTestId('ai-test-result')).not.toContainText('.pdf');

	// It survives a reload: the key and the choice are this browser's, not the session's.
	await page.reload();
	await expect(
		page.getByTestId('naming-section').getByRole('radio', { name: 'OpenAI-style' })
	).toBeChecked();
	await expect(page.getByTestId('ai-key')).toHaveValue('sk-not-a-real-key');
});

test('a refused key is reported in words, where it can be fixed', async ({ page }) => {
	await stubModel(page, { error: { message: 'Incorrect API key provided' } }, 401);
	const section = await openNaming(page);

	await section.getByRole('radio', { name: 'OpenAI-style' }).check();
	await page.getByTestId('ai-key').fill('sk-wrong');
	await page.getByTestId('ai-test').click();

	const result = page.getByTestId('ai-test-result');
	await expect(result).toHaveAttribute('role', 'alert');
	await expect(result).toContainText('rejected the API key');
});

test('there is nothing to test until there is a key', async ({ page }) => {
	const section = await openNaming(page);
	await section.getByRole('radio', { name: 'Anthropic' }).check();

	await expect(page.getByTestId('ai-test')).toBeDisabled();
	await page.getByTestId('ai-key').fill('sk-ant-something');
	await expect(page.getByTestId('ai-test')).toBeEnabled();
});

/** A scan sitting in review, which is where Find name lives. */
async function scanToReview(page: Page) {
	await page.goto('/');
	await page.getByTestId('scan-button').click();
	await expect(page).toHaveURL(/\/session\/[A-Za-z0-9-]+$/);
	await expect(page.getByTestId('session-state')).toHaveText('Ready to review', {
		timeout: 20_000
	});
}

test('Find name is offered even on a server that reads nothing', async ({ page }) => {
	await scanToReview(page);

	// The bug that started this: with naming.ocr off, the button used to be absent entirely.
	await expect(page.getByTestId('suggest-button')).toBeVisible();
	await expect(page.getByTestId('suggest-button')).toBeEnabled();

	await page.getByTestId('suggest-button').click();
	const popover = page.getByTestId('name-suggestions');
	await expect(popover).toBeVisible();
	// No model set up in this browser, so what is left is the template — and it is offered.
	await expect(popover.getByRole('button', { name: /scan/i }).first()).toBeVisible();

	// The button is a toggle. With a mouse the popover's own click-catcher takes the second
	// click and closes it either way; from the keyboard the button itself does.
	await page.getByTestId('suggest-button').focus();
	await page.keyboard.press('Enter');
	await expect(popover).toHaveCount(0);

	await page.getByTestId('suggest-button').click();
	await expect(popover).toBeVisible();
	await page.getByRole('button', { name: 'Close name suggestions' }).click();
	await expect(popover).toHaveCount(0);
});

test('a model set up in settings names the scan, and says so', async ({ page }) => {
	await stubModel(page, { choices: [{ message: { content: 'Quarterly Report Northwind' } }] });

	await page.goto('/settings');
	await page.getByTestId('naming-section').getByRole('radio', { name: 'OpenAI-style' }).check();
	await page.getByTestId('ai-key').fill('sk-not-a-real-key');

	await scanToReview(page);
	await page.getByTestId('suggest-button').click();

	const popover = page.getByTestId('name-suggestions');
	const suggestion = popover.getByRole('button', { name: /Quarterly Report Northwind/ });
	await expect(suggestion).toBeVisible({ timeout: 20_000 });

	// This server reads no text, so it was the page image that went — and it says which.
	await expect(popover.getByTestId('suggest-privacy')).toContainText('page image was sent');

	await suggestion.click();
	await expect(page.getByTestId('name-input')).toHaveValue('Quarterly Report Northwind');
	await expect(page.getByTestId('toast')).toContainText('Renamed to');
});

test('a refused key is reported in the popover, with the rows that did work', async ({ page }) => {
	await stubModel(page, { error: { message: 'nope' } }, 401);

	await page.goto('/settings');
	await page.getByTestId('naming-section').getByRole('radio', { name: 'OpenAI-style' }).check();
	await page.getByTestId('ai-key').fill('sk-wrong');

	await scanToReview(page);
	await page.getByTestId('suggest-button').click();

	await expect(page.getByTestId('suggest-error')).toContainText('rejected the API key');
	// Not a red bar over the whole screen, and not at the cost of the template row.
	await expect(
		page.getByTestId('name-suggestions').getByRole('button', { name: /scan/i }).first()
	).toBeVisible();
});

test('Find name waits its turn while a scan is running, and says why', async ({ page }) => {
	await page.goto('/');
	await page.getByTestId('scan-button').click();

	const button = page.getByTestId('suggest-button');
	await expect(button).toBeDisabled();
	await expect(page.getByTestId('suggest-button-wrap')).toHaveAttribute(
		'title',
		'Wait for the scan to finish'
	);
	await expect(button).toBeEnabled({ timeout: 20_000 });
});

test('a browser with no WebGPU is told so rather than left to fail', async ({ page }) => {
	await page.addInitScript(() => {
		// What Firefox, Safari and anything without a supported GPU look like.
		Object.defineProperty(Navigator.prototype, 'gpu', {
			get: () => undefined,
			configurable: true
		});
	});

	await page.goto('/settings');
	await page.getByTestId('naming-section').getByRole('radio', { name: 'In-browser' }).check();

	await expect(page.getByTestId('no-webgpu')).toContainText('no WebGPU');
	// And it does not go looking for six megabytes of runtime it cannot use.
	await expect(page.getByTestId('ai-local-model')).toHaveCount(0);
});

test('the model runtime is not on the bill for anyone who has not asked for it', async ({
	page
}) => {
	let workerBytes = 0;
	page.on('response', (r) => {
		if (r.url().includes('/workers/')) workerBytes += 1;
	});

	await page.goto('/');
	await page.goto('/settings');
	await expect(page.getByTestId('naming-section')).toBeVisible();

	// Six megabytes of WebGPU runtime, fetched only by the browser that opts into it.
	expect(workerBytes).toBe(0);
});
