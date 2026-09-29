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
