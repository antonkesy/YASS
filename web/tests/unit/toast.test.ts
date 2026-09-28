import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { get } from 'svelte/store';
import { TOAST_MS, toast } from '$lib/toast';

describe('toast', () => {
	beforeEach(() => vi.useFakeTimers());
	afterEach(() => {
		toast.dismiss();
		vi.useRealTimers();
	});

	it('shows a message and clears itself after the mockup lifetime', () => {
		toast.show('Saved');
		expect(get(toast)?.message).toBe('Saved');

		vi.advanceTimersByTime(TOAST_MS - 1);
		expect(get(toast)).not.toBeNull();
		vi.advanceTimersByTime(1);
		expect(get(toast)).toBeNull();
	});

	it('replaces rather than stacks, and restarts the clock', () => {
		toast.show('First');
		vi.advanceTimersByTime(TOAST_MS - 100);
		toast.show('Second');

		vi.advanceTimersByTime(200);
		expect(get(toast)?.message).toBe('Second');
		vi.advanceTimersByTime(TOAST_MS);
		expect(get(toast)).toBeNull();
	});

	it('carries an undo when one is offered', () => {
		const undo = vi.fn();
		toast.show('3 pages deleted', undo);

		get(toast)?.undo?.();
		expect(undo).toHaveBeenCalledOnce();
	});

	it('each toast gets its own id, so the entry animation replays', () => {
		const first = toast.show('a');
		const second = toast.show('b');
		expect(second).toBeGreaterThan(first);
	});
});
