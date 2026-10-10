import { defineConfig, devices } from '@playwright/test';

// E2E runs the real .NET server in --fake mode against ./build (via YASS_WEBROOT).
const port = 4173;
export const serverFolder = `${process.env.TMPDIR ?? '/tmp'}/yass-e2e/consume`;

export default defineConfig({
	testDir: 'tests/e2e',
	testMatch: '**/*.e2e.ts',
	timeout: 60_000,
	expect: { timeout: 10_000 },
	fullyParallel: false,
	workers: 1,
	reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
	use: { baseURL: `http://127.0.0.1:${port}`, trace: 'retain-on-failure' },
	projects: [
		{ name: 'desktop', use: { ...devices['Desktop Chrome'] } },
		{ name: 'mobile', use: { ...devices['Pixel 7'] } }
	],
	webServer: {
		// 400 ms/page so "stop a scan early" still has a running scan once review has slid in.
		// Two extra scanners so the picker and per-scanner presets have something to choose from.
		command:
			`pnpm build && dotnet run --project ../src/Yass.Server --no-launch-profile -- ` +
			`--fake 'pages=3,blank=2,delay=400ms,devices=Epson DS-530;Canon LiDE 400'`,
		url: `http://127.0.0.1:${port}/healthz`,
		timeout: 180_000,
		reuseExistingServer: !process.env.CI,
		env: {
			YASS__server__listen: `127.0.0.1:${port}`,
			YASS__sessions__directory: `${process.env.TMPDIR ?? '/tmp'}/yass-e2e/sessions`,
			YASS__output__directory: serverFolder,
			YASS__logging__level: 'warning',
			YASS_WEBROOT: `${import.meta.dirname}/build`
		}
	}
});
