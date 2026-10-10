// README screenshots against a running `--fake` server (YASS_URL, else the e2e server on :4173).
import { spawn, type ChildProcess } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { chromium, devices, type Page } from 'playwright';

const out = new URL('../../assets/screenshots/', import.meta.url).pathname;
const port = 4173;
const url = process.env.YASS_URL ?? `http://127.0.0.1:${port}`;

async function healthy(): Promise<boolean> {
	try {
		return (await fetch(`${url}/healthz`)).ok;
	} catch {
		return false;
	}
}

async function startServer(): Promise<ChildProcess> {
	const tmp = process.env.TMPDIR ?? '/tmp';
	const child = spawn(
		'dotnet',
		[
			'run',
			'--project',
			'../src/Yass.Server',
			'--no-launch-profile',
			'--',
			'--fake',
			'pages=6,blank=4,delay=300ms,devices=Epson DS-530;Canon LiDE 400'
		],
		{
			stdio: 'inherit',
			env: {
				...process.env,
				YASS__server__listen: `127.0.0.1:${port}`,
				YASS__sessions__directory: `${tmp}/yass-screenshots/sessions`,
				YASS__logging__level: 'warning',
				YASS_WEBROOT: new URL('../build', import.meta.url).pathname
			}
		}
	);
	const deadline = Date.now() + 180_000;
	while (!(await healthy())) {
		if (Date.now() > deadline) throw new Error('server did not come up');
		await new Promise((r) => setTimeout(r, 500));
	}
	return child;
}

async function settle(page: Page) {
	// Fonts, thumbnails and the stage image all arrive a beat after the DOM does.
	await page.waitForLoadState('networkidle');
	await page.evaluate(() => document.fonts.ready);
	await page.waitForTimeout(300);
}

async function shot(page: Page, name: string) {
	await settle(page);
	await page.screenshot({ path: `${out}${name}.png`, animations: 'disabled' });
	console.log(`  ${name}.png`);
}

async function scanToReview(page: Page) {
	await page.goto('/');
	const scan = page.getByTestId('scan-button');
	await scan.waitFor({ state: 'visible' });
	await page.waitForFunction(
		() => !document.querySelector('[data-testid=scan-button]')?.hasAttribute('disabled')
	);
	await scan.click();
	await page.waitForURL(/\/session\//);
	await page.getByTestId('filter-button').waitFor({ timeout: 30_000 });
	await page.locator('[data-testid=page-thumb] img').first().waitFor();
}

async function main() {
	mkdirSync(out, { recursive: true });
	const server = (await healthy()) ? null : await startServer();
	const browser = await chromium.launch();
	try {
		const phone = await browser.newContext({
			...devices['Pixel 7'],
			baseURL: url,
			colorScheme: 'light',
			reducedMotion: 'reduce'
		});
		// Headless Chromium offers the save dialog even on 127.0.0.1 and it cannot be driven;
		// without the API the button is a plain download, as on a real phone.
		await phone.addInitScript(() => {
			Object.defineProperty(window, 'showSaveFilePicker', { value: undefined });
		});
		const page = await phone.newPage();

		await scanToReview(page);
		await page.getByTestId('name-input').fill('Insurance letter');
		await page.getByTestId('name-input').press('Enter');
		await page.getByTestId('download-button').click();
		await page
			.getByTestId('session-state')
			.filter({ hasText: 'Downloaded' })
			.waitFor({ timeout: 20_000 });

		await page.goto('/');
		await page.getByTestId('session-list').waitFor();
		await shot(page, 'home');
		await page.getByTestId('past-scans').scrollIntoViewIfNeeded();
		await page
			.locator('[data-testid=session-tile] img')
			.first()
			.waitFor()
			.catch(() => {});
		await shot(page, 'past-scans');

		await scanToReview(page);
		await shot(page, 'review');

		await page.getByTestId('overview-open').click();
		await page.getByTestId('page-overview').waitFor();
		await page.locator('[data-testid=page-overview] [data-testid=page-thumb] img').last().waitFor();
		await shot(page, 'all-pages');
		await page.getByTestId('overview-close').click();

		await page.getByTestId('expand-button').click();
		await page.getByTestId('preview').waitFor();
		await page.locator('[data-testid=preview] img').waitFor();
		await shot(page, 'preview');
		await page.keyboard.press('Escape');

		await page.goto('/settings');
		await page.getByTestId('settings-form').waitFor();
		await shot(page, 'settings');
		await phone.close();

		const desktop = await browser.newContext({
			...devices['Desktop Chrome'],
			viewport: { width: 1280, height: 800 },
			baseURL: url,
			colorScheme: 'light',
			reducedMotion: 'reduce'
		});
		const wide = await desktop.newPage();
		await scanToReview(wide);
		await shot(wide, 'review-desktop');
		await desktop.close();
	} finally {
		await browser.close();
		server?.kill();
	}
}

main().catch((e) => {
	console.error(e);
	process.exit(1);
});
