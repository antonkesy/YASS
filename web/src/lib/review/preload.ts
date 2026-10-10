/**
 * Warms server-rendered previews into the browser's HTTP cache (URLs are immutable, keyed by
 * hash) via throwaway `Image`s; no bytes are kept in JS (SPEC §8).
 */
export const PRELOAD_LIMIT = 40;

export function preloadOrder(count: number, from: number, limit = PRELOAD_LIMIT): number[] {
	const order: number[] = [];
	for (let d = 1; order.length < limit && (from + d < count || from - d >= 0); d++) {
		if (from + d < count) order.push(from + d);
		if (order.length < limit && from - d >= 0) order.push(from - d);
	}
	return order;
}

export function loadImage(url: string): Promise<void> {
	return new Promise((resolve) => {
		const img = new Image();
		img.decoding = 'async';
		img.onload = img.onerror = () => resolve();
		img.src = url;
	});
}

/** `want` replaces the queue; low concurrency keeps the server's render slots free. */
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
		stop() {
			queue = [];
		}
	};
}
