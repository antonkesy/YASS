import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';
import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { SvelteKitPWA } from '@vite-pwa/sveltekit';

export default defineConfig({
	plugins: [
		tailwindcss(),
		sveltekit({
			compilerOptions: {
				// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
				runes: ({ filename }) =>
					filename.split(/[/\\]/).includes('node_modules') ? undefined : true
			},
			// SPA: one index.html served for every route by the .NET host.
			adapter: adapter({ fallback: 'index.html', pages: 'build', assets: 'build' })
		}),
		SvelteKitPWA({
			registerType: 'autoUpdate',
			strategies: 'generateSW',
			manifest: {
				name: 'YASS',
				short_name: 'YASS',
				description: 'Yet Another Scanner Service',
				theme_color: '#f5f7fc',
				background_color: '#f5f7fc',
				display: 'standalone',
				start_url: '/',
				icons: [
					{ src: 'icons/icon-192.png', sizes: '192x192', type: 'image/png' },
					{ src: 'icons/icon-512.png', sizes: '512x512', type: 'image/png' },
					{ src: 'icons/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' }
				]
			},
			workbox: {
				// Only the app shell is cached; scanner images and the hub are always live.
				globPatterns: ['client/**/*.{js,css,ico,png,svg,webp,woff2}'],
				navigateFallback: '/',
				navigateFallbackDenylist: [/^\/hub/, /^\/img\//, /^\/export\//, /^\/healthz/],
				runtimeCaching: []
			},
			devOptions: { enabled: false }
		})
	],
	// Under vitest, resolve Svelte's browser build so components mount in jsdom.
	resolve: process.env.VITEST ? { conditions: ['browser'] } : undefined,
	test: {
		expect: { requireAssertions: true },
		environment: 'jsdom',
		include: ['tests/unit/**/*.{test,spec}.ts', 'src/**/*.{test,spec}.ts'],
		setupFiles: ['tests/unit/setup.ts']
	}
});
