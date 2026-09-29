import { readdirSync } from 'node:fs';
import { expect, test, type Page } from '@playwright/test';
import { CPU_MODELS } from '../../src/lib/naming/cpu-models';

/** Where the e2e server writes saved documents; must match playwright.config.ts. */
const CONSUME_DIR = `${process.env.TMPDIR ?? '/tmp'}/yass-e2e/consume`;

// The server reads page text and has no provider of its own (see playwright.config.ts), which
// is the default everywhere: reading is local and on demand, asking a model is opt-in.

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

	// Find name is on out of the box, and the sheet says what the server brings to it.
	await expect(page.getByTestId('ocr-state')).toContainText('reads page text');
	// No provider chosen yet, so there is no key to ask for.
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

test('Find name is offered with no model set up at all', async ({ page }) => {
	await scanToReview(page);

	// No provider, no key: the heading off the page and the template are still worth having.
	await expect(page.getByTestId('name-suggest')).toBeVisible();
	await expect(page.getByTestId('name-suggest')).toBeEnabled();

	await page.getByTestId('name-suggest').click();
	const popover = page.getByTestId('name-suggestions');
	await expect(popover).toBeVisible();
	// No model set up in this browser, so what is left is the template — and it is offered.
	await expect(popover.getByRole('button', { name: /scan/i }).first()).toBeVisible();

	// The button is a toggle. With a mouse the popover's own click-catcher takes the second
	// click and closes it either way; from the keyboard the button itself does.
	await page.getByTestId('name-suggest').focus();
	await page.keyboard.press('Enter');
	await expect(popover).toHaveCount(0);

	await page.getByTestId('name-suggest').click();
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
	await page.getByTestId('name-suggest').click();

	const popover = page.getByTestId('name-suggestions');
	const suggestion = popover.getByRole('button', { name: /Quarterly Report Northwind/ });
	await expect(suggestion).toBeVisible({ timeout: 20_000 });

	// The text is what went, and the popover says so rather than leaving it to be assumed.
	await expect(popover.getByTestId('suggest-privacy')).toContainText('text read from the page');

	await suggestion.click();
	await expect(page.getByTestId('document-name')).toHaveAttribute(
		'data-name',
		'Quarterly Report Northwind'
	);
	await expect(page.getByTestId('toast')).toContainText('Renamed to');
});

test('a refused key is reported in the popover, with the rows that did work', async ({ page }) => {
	await stubModel(page, { error: { message: 'nope' } }, 401);

	await page.goto('/settings');
	await page.getByTestId('naming-section').getByRole('radio', { name: 'OpenAI-style' }).check();
	await page.getByTestId('ai-key').fill('sk-wrong');

	await scanToReview(page);
	await page.getByTestId('name-suggest').click();

	await expect(page.getByTestId('suggest-error')).toContainText('rejected the API key');
	// Not a red bar over the whole screen, and not at the cost of the template row.
	await expect(
		page.getByTestId('name-suggestions').getByRole('button', { name: /scan/i }).first()
	).toBeVisible();
});

test('Find name waits its turn while a scan is running, and says why', async ({ page }) => {
	await page.goto('/');
	await page.getByTestId('scan-button').click();

	await expect(page.getByTestId('session-state')).toHaveText('Scanning');
	const button = page.getByTestId('name-suggest');
	await expect(button).toBeDisabled();
	await expect(button).toHaveAttribute('title', 'Wait for the scan to finish');
	await expect(button).toBeEnabled({ timeout: 20_000 });
});

/** What Firefox, Safari and anything without a supported GPU look like. */
async function withoutWebGpu(page: Page) {
	await page.addInitScript(() => {
		Object.defineProperty(Navigator.prototype, 'gpu', {
			get: () => undefined,
			configurable: true
		});
	});
}

test('a browser with no WebGPU is sent to the processor, not to an API key', async ({ page }) => {
	await withoutWebGpu(page);

	await page.goto('/settings');
	const naming = page.getByTestId('naming-section');
	await naming.getByRole('radio', { name: 'In-browser (GPU)' }).check();

	await expect(page.getByTestId('no-webgpu')).toContainText('no WebGPU');
	await expect(page.getByTestId('no-webgpu')).toContainText('In-browser (CPU)');
	// And it does not go looking for six megabytes of runtime it cannot use.
	await expect(page.getByTestId('ai-local-model')).toHaveCount(0);
});

// The stubbed model answers the same thing every time, so each style has one right answer.
for (const [style, expected] of [
	['As written', 'Quarterly Report Northwind'],
	['lower_snake', 'quarterly_report_northwind'],
	['lower-kebab', 'quarterly-report-northwind']
]) {
	test(`a found name lands inside the template, written ${style}`, async ({ page }) => {
		await stubModel(page, { choices: [{ message: { content: 'Quarterly Report Northwind' } }] });

		const section = await openNaming(page);
		await section.getByRole('radio', { name: 'OpenAI-style' }).check();
		await page.getByTestId('ai-key').fill('sk-not-a-real-key');
		await page.getByTestId('template-input').fill('{YEAR}-{MONTH}-{DAY}_{NAME}');
		await page.getByRole('radio', { name: style }).check();

		await scanToReview(page);
		await page.getByTestId('name-suggest').click();

		// The row stays readable whatever the style: the convention is for the file name.
		const row = page
			.getByTestId('name-suggestions')
			.getByRole('button', { name: /Quarterly Report Northwind/ });
		await expect(row).toBeVisible({ timeout: 20_000 });
		await row.click();

		// The date came from the template, the words from the model, joined as asked.
		const today = new Date();
		const date = [
			today.getFullYear(),
			String(today.getMonth() + 1).padStart(2, '0'),
			String(today.getDate()).padStart(2, '0')
		].join('-');
		await expect(page.getByTestId('document-name')).toHaveAttribute(
			'data-name',
			`${date}_${expected}`
		);
	});
}

test('the styled name in the template is what lands on disk', async ({ page }) => {
	await stubModel(page, { choices: [{ message: { content: 'Quarterly Report Northwind' } }] });

	const section = await openNaming(page);
	await section.getByRole('radio', { name: 'OpenAI-style' }).check();
	await page.getByTestId('ai-key').fill('sk-not-a-real-key');
	await page.getByTestId('template-input').fill('{YEAR}-{MONTH}-{DAY}_{NAME}');
	await page.getByRole('radio', { name: 'lower-kebab' }).check();

	await scanToReview(page);
	await page.getByTestId('name-suggest').click();
	await page
		.getByTestId('name-suggestions')
		.getByRole('button', { name: /Quarterly Report Northwind/ })
		.click();

	// The name field is one thing; the file the server writes is the one that counts, and it
	// is the server that expands whatever the browser hands it.
	await page.getByTestId('save-button').click();
	await expect(page.getByTestId('saved-message')).toBeVisible({ timeout: 20_000 });

	expect(
		readdirSync(CONSUME_DIR).some((f) =>
			/^\d{4}-\d{2}-\d{2}_quarterly-report-northwind(-\d+)?\.pdf$/.test(f)
		)
	).toBe(true);
});

test('a template with nothing found in it never writes {NAME} into a file', async ({ page }) => {
	await openNaming(page);
	await page.getByTestId('template-input').fill('{YEAR}-{MONTH}-{DAY}_{NAME}');

	// No suggestion picked, so the template itself is what the server is asked to expand —
	// and the server writes an unknown placeholder out as it found it. This is the one way
	// a browser-only token reaches a real file name, so the file on disk is what is checked.
	await scanToReview(page);
	await page.getByTestId('save-button').click();
	await expect(page.getByTestId('saved-message')).toBeVisible({ timeout: 20_000 });

	const written = readdirSync(CONSUME_DIR);
	expect(written.some((f) => f.includes('{NAME}'))).toBe(false);
	expect(written.some((f) => /^\d{4}-\d{2}-\d{2}\.pdf$/.test(f))).toBe(true);
});

test('a finished scan reads as the template, with the name left as a slot', async ({ page }) => {
	await openNaming(page);
	await page.getByTestId('template-input').fill('{YEAR}-{MONTH}-{DAY}_{NAME}');

	await scanToReview(page);

	// Nothing has been read: the name is the template, and where {NAME} sits is a slot that
	// says so rather than a name nobody asked for.
	await expect(page.getByTestId('document-name')).toHaveAttribute(
		'data-name',
		/^\d{4}-\d{2}-\d{2}$/
	);
	await expect(page.getByTestId('name-generated')).toHaveText(/name it/);
});

test('pressing for a name fills the slot, and the rest of the template stays', async ({ page }) => {
	await stubModel(page, { choices: [{ message: { content: 'Quarterly Report Northwind' } }] });

	const section = await openNaming(page);
	await section.getByRole('radio', { name: 'OpenAI-style' }).check();
	await page.getByTestId('ai-key').fill('sk-not-a-real-key');
	await page.getByTestId('template-input').fill('{YEAR}-{MONTH}-{DAY}_{NAME}');
	await page.getByRole('radio', { name: 'lower-kebab' }).check();

	await scanToReview(page);
	await page.getByTestId('name-generated').click();
	await page
		.getByTestId('name-suggestions')
		.getByRole('button', { name: /Quarterly Report Northwind/ })
		.click();

	const today = new Date();
	const date = [
		today.getFullYear(),
		String(today.getMonth() + 1).padStart(2, '0'),
		String(today.getDate()).padStart(2, '0')
	].join('-');
	await expect(page.getByTestId('document-name')).toHaveAttribute(
		'data-name',
		`${date}_quarterly-report-northwind`
	);
});

test('a name typed by hand replaces the slot and survives a reload', async ({ page }) => {
	await openNaming(page);
	await page.getByTestId('template-input').fill('{YEAR}-{MONTH}-{DAY}_{NAME}');

	await scanToReview(page);
	await page.getByTestId('name-generated').click();
	await page.getByTestId('suggest-mine').click();
	await page.getByTestId('name-input').fill('Mine');
	await page.getByTestId('name-input').press('Enter');

	const name = page.getByTestId('document-name');
	await expect(name).toHaveAttribute('data-name', 'Mine');
	// The slot is gone: a name that no longer fits the template is nobody's but yours.
	await expect(page.getByTestId('name-generated')).toHaveCount(0);

	await page.reload();
	await expect(page.getByTestId('document-name')).toHaveAttribute('data-name', 'Mine');
});

test('the template menu offers whole templates, shown as what they produce', async ({ page }) => {
	await openNaming(page);
	await page.getByTestId('template-presets').click();

	const menu = page.getByRole('menu');
	await expect(menu.getByRole('menuitem')).toHaveCount(5);

	// Each row says what it would write, not just the tokens it is made of.
	await menu.getByRole('menuitem').filter({ hasText: '{NAME}' }).first().click();
	await expect(page.getByTestId('template-input')).toHaveValue(/\{NAME\}/);

	// And the preview points at the part a found name fills.
	await expect(page.getByTestId('template-preview-name')).toBeVisible();
});

test('turning Find name off hides it, and then nothing reads the page', async ({ page }) => {
	const section = await openNaming(page);
	await expect(section.getByRole('radio', { name: 'OpenAI-style' })).toBeVisible();

	await section.getByRole('checkbox', { name: /^Find name/ }).uncheck();

	// The provider choice goes with it: all of it was configuring the one feature.
	await expect(section.getByRole('radio', { name: 'OpenAI-style' })).toHaveCount(0);
	await expect(page.getByTestId('find-name-off')).toBeVisible();

	await scanToReview(page);
	await expect(page.getByTestId('name-suggest')).toHaveCount(0);

	// And it comes back, because this is a preference and not a one-way door.
	await openNaming(page);
	await page
		.getByTestId('naming-section')
		.getByRole('checkbox', { name: /^Find name/ })
		.check();
	await scanToReview(page);
	await expect(page.getByTestId('name-suggest')).toBeVisible();
});

test('the CPU engine offers its models to a browser with no WebGPU at all', async ({ page }) => {
	await withoutWebGpu(page);

	await page.goto('/settings');
	const naming = page.getByTestId('naming-section');
	await naming.getByRole('radio', { name: 'In-browser (CPU)' }).check();

	// No dead end this time: a catalogue, on the same browser that was just turned away.
	await expect(page.getByTestId('no-webgpu')).toHaveCount(0);
	const models = page.getByTestId('ai-local-model');
	// The catalogue plus the "Choose a model…" placeholder, with each size beside its name.
	await expect(models.locator('option')).toHaveCount(CPU_MODELS.length + 1);
	await expect(models).toContainText(CPU_MODELS[0].label);

	// Nothing can be downloaded until something is chosen, and a URL of one's own is offered.
	await expect(page.getByTestId('ai-download')).toBeDisabled();
	await expect(page.getByTestId('ai-gguf-url')).toBeVisible();

	await models.selectOption({ index: 1 });
	await expect(page.getByTestId('ai-download')).toBeEnabled();
});

test.describe('cross-origin isolation', () => {
	// A model on the processor only gets more than one thread on an isolated page: threads need
	// SharedArrayBuffer, and a browser only hands that to a page that asked on both headers.
	for (const path of ['/', '/settings', '/session/20260929-000000-000000']) {
		test(`${path} asks the browser to isolate it`, async ({ page }) => {
			const response = await page.goto(path);
			const headers = response?.headers() ?? {};

			expect(headers['cross-origin-opener-policy']).toBe('same-origin');
			expect(headers['cross-origin-embedder-policy']).toBe('require-corp');
		});
	}

	test('and the browser agrees, so a model here can use more than one thread', async ({ page }) => {
		// The deep link matters on its own: it is the SPA fallback rather than a file on disk,
		// a separate route from "/", and it is the page that turns the model on.
		await page.goto('/settings');

		expect(await page.evaluate(() => self.crossOriginIsolated)).toBe(true);
		expect(await page.evaluate(() => typeof SharedArrayBuffer !== 'undefined')).toBe(true);
		// What wllama itself checks before it asks for a thread pool.
		expect(
			await page.evaluate(() => {
				try {
					new MessageChannel().port1.postMessage(new SharedArrayBuffer(1));
					return true;
				} catch {
					return false;
				}
			})
		).toBe(true);
	});

	test('the scanner images the review screen lives on still load', async ({ page }) => {
		// require-corp blocks a cross-origin subresource that has not opted in. These are
		// same-origin, so they must be unaffected — but that is worth proving, not assuming.
		await scanToReview(page);
		const thumb = page.getByTestId('page-thumb').first();
		await expect(thumb).toBeVisible();
		expect(
			await thumb.locator('img').evaluate((img: HTMLImageElement) => img.naturalWidth)
		).toBeGreaterThan(0);
	});
});

test('a model runtime is not on the bill for anyone who has not asked for it', async ({ page }) => {
	const fetched: string[] = [];
	page.on('response', (r) => {
		const url = r.url();
		if (url.includes('/workers/') || url.endsWith('.wasm')) fetched.push(url);
	});

	await page.goto('/');
	await page.goto('/settings');
	await expect(page.getByTestId('naming-section')).toBeVisible();

	// Six megabytes of WebGPU runtime and eight of WebAssembly, fetched only when opted into.
	expect(fetched).toEqual([]);
});
