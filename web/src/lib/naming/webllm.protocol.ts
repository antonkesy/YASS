/**
 * What the page and the model worker say to each other. Types only, so importing this costs
 * the page nothing — which is the whole point of the worker owning the package.
 */

export type LocalModel = { id: string; sizeMb: number };

export type ToWorker =
	| { type: 'models'; id: number }
	| { type: 'load'; id: number; model: string }
	| { type: 'name'; id: number; model: string; system: string; user: string; maxTokens: number };

/** A request without the id the caller has not assigned yet, distributed over the union. */
export type Request<T = ToWorker> = T extends { id: number } ? Omit<T, 'id'> : never;

export type FromWorker =
	| { type: 'progress'; percent: number; text: string }
	| { type: 'done'; id: number; models?: LocalModel[]; text?: string | null }
	| { type: 'failed'; id: number; error: string };
