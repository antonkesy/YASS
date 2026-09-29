import type { ServerResponse } from 'node:http';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig, type Plugin } from 'vitest/config';
import adapter from '@sveltejs/adapter-static';
import { sveltekit } from '@sveltejs/kit/vite';
import { SvelteKitPWA } from '@vite-pwa/sveltekit';

/**
 * What the .NET host sends in production (StaticFrontend.cs), so that `vite dev` and
 * `vite preview` are cross-origin isolated too and the in-browser CPU model gets its threads
 * here as well (ADR-011). Without it, development is quietly single-threaded and production is
 * not — the kind of difference that is only ever found the slow way.
 *
 * A middleware rather than `server.headers`, because SvelteKit serves pages from its own dev
 * handler which those never reach — and registered directly rather than from a returned hook,
 * so that it runs before Vite's own middlewares and therefore covers module scripts too. A
 * worker of an isolated document has to carry the headers itself or the browser refuses to
 * start it, so missing the scripts is the same as not having done this at all.
 */
const crossOriginIsolation: Plugin = {
	name: 'yass-cross-origin-isolation',
	configureServer(server) {
		server.middlewares.use(isolate);
	},
	configurePreviewServer(server) {
		server.middlewares.use(isolate);
	}
};

function isolate(_req: unknown, res: ServerResponse, next: () => void) {
	res.setHeader('Cross-Origin-Opener-Policy', 'same-origin');
	res.setHeader('Cross-Origin-Embedder-Policy', 'require-corp');
	next();
}

export default defineConfig({
	plugins: [
		crossOriginIsolation,
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
				// The in-browser naming model is megabytes of WebGPU runtime that most people
				// never turn on (ADR-010), and it lives entirely in a worker so that the page
				// never loads it. Precaching it would undo that; it is fetched on demand, like
				// the weights it goes on to download.
				globIgnores: ['**/workers/**'],
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
