import { defineConfig, devices } from '@playwright/test';

/**
 * E2E runs against the real .NET server in --fake mode, serving the production build.
 * `pnpm build` writes to ./build; the server is pointed at it through YAWSS_WEBROOT.
 */
const port = 4173;

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
		command: `pnpm build && dotnet run --project ../src/Yawss.Server --no-launch-profile -- --fake pages=3,blank=2,delay=150ms`,
		url: `http://127.0.0.1:${port}/healthz`,
		timeout: 180_000,
		reuseExistingServer: !process.env.CI,
		env: {
			YAWSS__server__listen: `127.0.0.1:${port}`,
			YAWSS__sessions__directory: `${process.env.TMPDIR ?? '/tmp'}/yawss-e2e/sessions`,
			YAWSS__output__directory: `${process.env.TMPDIR ?? '/tmp'}/yawss-e2e/consume`,
			YAWSS__logging__level: 'warning',
			YAWSS_WEBROOT: `${import.meta.dirname}/build`
		}
	}
});
