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
	// One viewport: these check that the paths work end to end, not how they look.
	projects: [{ name: 'desktop', use: { ...devices['Desktop Chrome'] } }],
	webServer: {
		// 1 s/page so "stop a scan" still has a running scan once review has slid in.
		command:
			`pnpm build && dotnet run --project ../src/Yass.Server --no-launch-profile -- ` +
			`--fake 'pages=3,blank=2,delay=1s'`,
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
