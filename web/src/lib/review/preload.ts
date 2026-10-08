/**
 * Page previews are rendered by the server the first time they are asked for, which is the
 * pause you see when stepping to a page nobody has looked at yet. Asking ahead of time, nearest
 * pages first, means that render has already happened by the time you get there.
 *
 * Nothing is kept in JS: each request is a throwaway `Image` whose bytes land in the browser's
 * HTTP cache (the preview URLs are immutable, keyed by hash), so the `<img>` that later asks for
 * the same URL is served from there. SPEC §8 keeps image bytes out of JS beyond the current page.
 */

/** Most pages warmed around the one on screen; a 500-page scan should not render all 500. */
export const PRELOAD_LIMIT = 40;

/**
 * Page indices in the order worth fetching them: the next page, the previous one, then
 * outwards — forwards first, because going through a scan is mostly forwards.
 */
export function preloadOrder(count: number, from: number, limit = PRELOAD_LIMIT): number[] {
	const order: number[] = [];
	for (let d = 1; order.length < limit && (from + d < count || from - d >= 0); d++) {
		if (from + d < count) order.push(from + d);
		if (order.length < limit && from - d >= 0) order.push(from - d);
	}
	return order;
}

/** Fetch one URL into the browser cache. Resolves either way: a failed preload is not an error. */
export function loadImage(url: string): Promise<void> {
	return new Promise((resolve) => {
		const img = new Image();
		img.decoding = 'async';
		img.onload = img.onerror = () => resolve();
		img.src = url;
	});
}

/**
 * A small queue of URLs to warm. `want` replaces what is waiting — moving to another page
 * re-ranks everything around it — while requests already in flight are left to finish.
 * At most `concurrency` at once, so the server's render slots stay free for what is on screen.
 */
export function createPreloader(load: (url: string) => Promise<void> = loadImage, concurrency = 2) {
	const started = new Set<string>();
	let queue: string[] = [];
	let active = 0;

	function pump() {
		while (active < concurrency && queue.length > 0) {
			const url = queue.shift()!;
			if (started.has(url)) continue;
			started.add(url);
			active++;
			void load(url).finally(() => {
				active--;
				pump();
			});
		}
	}

	return {
		want(urls: readonly string[]) {
			queue = urls.filter((u) => !started.has(u));
			pump();
		},
		/** Stop queueing; whatever is in flight finishes on its own. */
		stop() {
			queue = [];
		}
	};
}
