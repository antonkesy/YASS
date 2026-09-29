import { NamingError } from './types';

/**
 * The page's half of a model worker, for both engines (ADR-010, ADR-011).
 *
 * Neither engine's package is imported here, and that is the point: this file is all the page
 * ever loads to talk to one. The worker owns the megabytes; the page owns an id counter and a
 * map of promises.
 */

/** Where a download or a decode has got to, for the button label and the popover's loading line. */
export type Progress = (phase: 'downloading' | 'thinking', detail?: string) => void;

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

		worker = spawn();
		worker.onmessage = (event: MessageEvent<Receive>) => {
			const message = event.data;
			if (message.type === 'progress') {
				onProgress?.('downloading', `${message.percent}%`);
				return;
			}

			const pending = waiting.get(message.id);
			waiting.delete(message.id);
			if (!pending) return;
			if (message.type === 'failed') pending.reject(new NamingError(message.error));
			else pending.resolve(message);
		};
		worker.onerror = () => {
			for (const pending of waiting.values()) {
				pending.reject(new NamingError('The model worker stopped unexpectedly.'));
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
