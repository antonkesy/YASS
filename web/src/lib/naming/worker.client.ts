import { NamingError } from './types';

/**
 * The page's half of a model worker, for both engines (ADR-010, ADR-011).
 *
 * Neither engine's package is imported here, and that is the point: this file is all the page
 * ever loads to talk to one. The worker owns the megabytes; the page owns an id counter and a
 * map of promises.
 */

/**
 * Where a download or a decode has got to. The percent is what the engine itself reports, and
 * it is a number rather than a formatted string because both callers draw a bar with it.
 */
export type Progress = (phase: 'downloading' | 'thinking', percent?: number) => void;

/** The shape both protocols share: every message carries an id, except progress. */
type Reply =
	| { type: 'progress'; percent: number }
	| { type: 'done'; id: number }
	| { type: 'failed'; id: number; error: string };

/** A request without the id the caller has not assigned yet, distributed over the union. */
export type Request<T> = T extends { id: number } ? Omit<T, 'id'> : never;

export type WorkerClient<Send, Receive> = {
	ask(request: Request<Send>, progress?: Progress): Promise<Receive>;
};

/**
 * One worker, spawned on the first question and kept for the rest of the session so the weights
 * are loaded once. `spawn` is a thunk rather than a URL because `new Worker(new URL(…))` only
 * works when Vite can see it literally at the call site.
 */
export function workerClient<Send extends { id: number }, Receive extends Reply>(
	spawn: () => Worker
): WorkerClient<Send, Receive> {
	let worker: Worker | null = null;
	let nextId = 0;
	const waiting = new Map<number, { resolve: (m: Receive) => void; reject: (e: Error) => void }>();
	let onProgress: Progress | undefined;

	function connect(): Worker {
		if (worker) return worker;

		let spoke = false;

		worker = spawn();
		worker.onmessage = (event: MessageEvent<Receive>) => {
			spoke = true;
			const message = event.data;
			if (message.type === 'progress') {
				onProgress?.('downloading', message.percent);
				return;
			}

			const pending = waiting.get(message.id);
			waiting.delete(message.id);
			if (!pending) return;
			if (message.type === 'failed') pending.reject(new NamingError(message.error));
			else pending.resolve(message);
		};
		worker.onerror = () => {
			// A worker that never said anything did not crash, it failed to start — and the browser
			// gives an error event with nothing in it either way. The distinction is worth drawing,
			// because on an isolated page the usual cause is a copy of the worker cached from before
			// isolation was turned on: the headers are part of what the browser checks, a year-long
			// immutable cache does not revalidate, and the symptom is otherwise unreadable.
			const reason = spoke
				? 'The model worker stopped unexpectedly.'
				: 'The model worker would not start.' +
					(globalThis.crossOriginIsolated
						? ' If this browser ran YASS before, a cached copy from then is the likely cause; reload the page with the cache emptied.'
						: '');

			for (const pending of waiting.values()) {
				pending.reject(new NamingError(reason));
			}
			waiting.clear();
			worker?.terminate();
			worker = null;
		};

		return worker;
	}

	return {
		ask(request, progress) {
			const id = ++nextId;
			onProgress = progress;
			return new Promise((resolve, reject) => {
				waiting.set(id, { resolve, reject });
				connect().postMessage({ ...request, id } as unknown as Send);
			});
		}
	};
}
