/**
 * What the page and the CPU model worker say to each other. Types only, so importing this
 * costs the page nothing — which is the whole point of the worker owning the package.
 *
 * Shorter than the GPU engine's protocol by one message: there is no `models`, because the
 * catalogue is a list of ours in `cpu-models.ts` rather than something the package knows.
 */

export type ToWorker =
	| { type: 'load'; id: number; model: string }
	| { type: 'name'; id: number; model: string; system: string; user: string; maxTokens: number };

export type FromWorker =
	| { type: 'progress'; percent: number }
	| { type: 'done'; id: number; text?: string | null }
	| { type: 'failed'; id: number; error: string };
