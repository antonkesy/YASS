import { afterEach, describe, expect, it, vi } from 'vitest';
import { NamingError } from '$lib/naming';
import { workerClient } from '$lib/naming/worker.client';

/**
 * The page's half of a model worker, without either engine's package. jsdom has no `Worker`,
 * which is convenient: the fake below is the whole other side of the protocol.
 */

type Sent = { type: 'load' | 'name'; id: number };
type Got =
	| { type: 'progress'; percent: number }
	| { type: 'done'; id: number; text?: string }
	| { type: 'failed'; id: number; error: string };

/** A worker that records what it was posted and lets the test answer whenever it likes. */
class FakeWorker {
	static made: FakeWorker[] = [];
	posted: Sent[] = [];
	terminated = false;
	onmessage: ((e: MessageEvent<Got>) => void) | null = null;
	onerror: (() => void) | null = null;

	constructor() {
		FakeWorker.made.push(this);
	}

	postMessage(message: Sent) {
		this.posted.push(message);
	}

	terminate() {
		this.terminated = true;
	}

	/** What the real worker does on its way back. */
	reply(message: Got) {
		this.onmessage?.({ data: message } as MessageEvent<Got>);
	}
}

function client() {
	return workerClient<Sent, Got>(() => new FakeWorker() as unknown as Worker);
}

afterEach(() => {
	FakeWorker.made = [];
});

describe('the page half of a model worker', () => {
	it('spawns nothing until something is asked, then keeps the one worker', async () => {
		const c = client();
		expect(FakeWorker.made).toHaveLength(0);

		const first = c.ask({ type: 'load' });
		const second = c.ask({ type: 'name' });
		expect(FakeWorker.made).toHaveLength(1);

		const worker = FakeWorker.made[0];
		worker.reply({ type: 'done', id: 2, text: 'second' });
		worker.reply({ type: 'done', id: 1, text: 'first' });

		// Answers come back out of order, and each one still finds its own caller.
		await expect(first).resolves.toMatchObject({ text: 'first' });
		await expect(second).resolves.toMatchObject({ text: 'second' });
	});

	it('gives each request an id the caller did not have to think about', async () => {
		const c = client();
		const pending = c.ask({ type: 'load' });
		const worker = FakeWorker.made[0];

		expect(worker.posted).toEqual([{ type: 'load', id: 1 }]);
		worker.reply({ type: 'done', id: 1 });
		await pending;
	});

	it('routes progress to the callback rather than to a waiting caller', async () => {
		const c = client();
		const seen: [string, string | undefined][] = [];
		const pending = c.ask({ type: 'load' }, (phase, detail) => seen.push([phase, detail]));
		const worker = FakeWorker.made[0];

		worker.reply({ type: 'progress', percent: 42 });
		worker.reply({ type: 'progress', percent: 100 });
		worker.reply({ type: 'done', id: 1 });

		await pending;
		expect(seen).toEqual([
			['downloading', '42%'],
			['downloading', '100%']
		]);
	});

	it('turns a failure into a sentence fit to put on screen', async () => {
		const c = client();
		const pending = c.ask({ type: 'load' });
		FakeWorker.made[0].reply({ type: 'failed', id: 1, error: 'Out of memory.' });

		await expect(pending).rejects.toThrow(NamingError);
		await expect(pending).rejects.toThrow('Out of memory.');
	});

	it('does not leave callers hanging when the worker dies', async () => {
		const c = client();
		const first = c.ask({ type: 'load' });
		const second = c.ask({ type: 'name' });
		const worker = FakeWorker.made[0];

		// It answered once, so it ran and then fell over.
		worker.reply({ type: 'progress', percent: 10 });
		worker.onerror?.();

		await expect(first).rejects.toThrow('The model worker stopped unexpectedly.');
		await expect(second).rejects.toThrow('The model worker stopped unexpectedly.');
		expect(worker.terminated).toBe(true);

		// And the next question gets a fresh one rather than the corpse.
		const third = c.ask({ type: 'load' });
		expect(FakeWorker.made).toHaveLength(2);
		FakeWorker.made[1].reply({ type: 'done', id: 3 });
		await third;
	});

	it('tells a worker that never started apart from one that crashed', async () => {
		const c = client();
		const pending = c.ask({ type: 'load' });

		// Not a word before the error: it never ran, which is a different problem with a
		// different fix, and the browser's own error event says nothing either way.
		FakeWorker.made[0].onerror?.();

		await expect(pending).rejects.toThrow('would not start');
	});

	it('names the cached-worker trap when the page is cross-origin isolated', async () => {
		vi.stubGlobal('crossOriginIsolated', true);
		const c = client();
		const pending = c.ask({ type: 'load' });
		FakeWorker.made[0].onerror?.();

		await expect(pending).rejects.toThrow(/cached copy/);
		vi.unstubAllGlobals();
	});

	it('ignores an answer to a question nobody asked', async () => {
		const c = client();
		const pending = c.ask({ type: 'load' });
		const worker = FakeWorker.made[0];

		expect(() => worker.reply({ type: 'done', id: 99 })).not.toThrow();
		worker.reply({ type: 'done', id: 1, text: 'mine' });
		await expect(pending).resolves.toMatchObject({ text: 'mine' });
	});
});

describe('the GPU engine', () => {
	it('says so rather than spawning a worker when the browser has no WebGPU', async () => {
		vi.stubGlobal('navigator', { ...navigator, gpu: undefined });
		const { hasWebGpu, localModels, askWebLlm } = await import('$lib/naming/webllm');

		expect(hasWebGpu()).toBe(false);
		await expect(localModels()).resolves.toEqual([]);
		await expect(askWebLlm({ kind: 'text', text: 'x', pages: 1 }, 'a-model')).rejects.toThrow(
			'no WebGPU'
		);
		vi.unstubAllGlobals();
	});
});
