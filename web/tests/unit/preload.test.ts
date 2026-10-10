import { describe, expect, it } from 'vitest';
import { createPreloader, preloadOrder } from '$lib/review/preload';

describe('preloadOrder', () => {
	it('goes outwards from the page in view, forwards first', () => {
		expect(preloadOrder(6, 2)).toEqual([3, 1, 4, 0, 5]);
	});

	it('runs one way at either end of the scan', () => {
		expect(preloadOrder(4, 0)).toEqual([1, 2, 3]);
		expect(preloadOrder(4, 3)).toEqual([2, 1, 0]);
	});

	it('stops at the limit, nearest pages kept', () => {
		expect(preloadOrder(100, 50, 4)).toEqual([51, 49, 52, 48]);
	});

	it('has nothing to fetch for a single page', () => {
		expect(preloadOrder(1, 0)).toEqual([]);
	});
});

describe('createPreloader', () => {
	function controlled() {
		const pending = new Map<string, () => void>();
		const asked: string[] = [];
		const load = (url: string) =>
			new Promise<void>((resolve) => {
				asked.push(url);
				pending.set(url, resolve);
			});
		const finish = async (url: string) => {
			pending.get(url)?.();
			pending.delete(url);
			await Promise.resolve();
			await Promise.resolve();
		};
		return { load, asked, finish };
	}

	it('keeps at most two requests in flight, in the order asked', async () => {
		const c = controlled();
		const preloader = createPreloader(c.load, 2);

		preloader.want(['a', 'b', 'c']);
		expect(c.asked).toEqual(['a', 'b']);

		await c.finish('a');
		expect(c.asked).toEqual(['a', 'b', 'c']);
	});

	it('re-ranks what is waiting, and never asks for the same page twice', async () => {
		const c = controlled();
		const preloader = createPreloader(c.load, 1);

		preloader.want(['a', 'b', 'c']);
		preloader.want(['c', 'a', 'd']);
		await c.finish('a');
		await c.finish('c');

		expect(c.asked).toEqual(['a', 'c', 'd']);
	});

	it('queues nothing more once stopped', async () => {
		const c = controlled();
		const preloader = createPreloader(c.load, 1);

		preloader.want(['a', 'b']);
		preloader.stop();
		await c.finish('a');

		expect(c.asked).toEqual(['a']);
	});
});
