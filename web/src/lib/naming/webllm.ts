import { cleanName } from './clean';
import { INSTRUCTION, MAX_TOKENS, question } from './prompt';
import { NamingError, type NamingInput } from './types';
import type { FromWorker, LocalModel, Request, ToWorker } from './webllm.protocol';

/**
 * The page's half of the in-browser model (ADR-010). It knows how to ask; the worker owns the
 * package that answers, and nothing here imports it — that is what keeps six megabytes of
 * WebGPU runtime off the first visit of everyone who never turns this on.
 */

export type Progress = (phase: 'downloading' | 'thinking', detail?: string) => void;
export type { LocalModel };

/** Whether this browser can run a model at all. */
export function hasWebGpu(): boolean {
	return typeof navigator !== 'undefined' && navigator.gpu != null;
}

let worker: Worker | null = null;
let nextId = 0;
const waiting = new Map<number, { resolve: (m: FromWorker) => void; reject: (e: Error) => void }>();
let onProgress: Progress | undefined;

function connect(): Worker {
	if (worker) return worker;

	worker = new Worker(new URL('./webllm.worker.ts', import.meta.url), { type: 'module' });
	worker.onmessage = (event: MessageEvent<FromWorker>) => {
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

function ask(request: Request, progress?: Progress): Promise<FromWorker> {
	const id = ++nextId;
	onProgress = progress;
	return new Promise((resolve, reject) => {
		waiting.set(id, { resolve, reject });
		connect().postMessage({ ...request, id } as ToWorker);
	});
}

/** The models the package ships, smallest first, with its own size figures rather than ours. */
export async function localModels(): Promise<LocalModel[]> {
	if (!hasWebGpu()) return [];
	const answer = await ask({ type: 'models' });
	return answer.type === 'done' ? (answer.models ?? []) : [];
}

/**
 * Downloads a model without asking it anything, so it can be done deliberately in settings
 * rather than discovered halfway through naming a scan.
 */
export async function loadModel(modelId: string, progress?: Progress): Promise<void> {
	if (!hasWebGpu()) throw new NamingError('This browser has no WebGPU.');
	try {
		await ask({ type: 'load', model: modelId }, progress);
	} catch (e) {
		// Whatever it was — a GPU that lacks a feature, weights that would not download —
		// it said so itself, and that is more use than a sentence of ours over the top.
		throw new NamingError(
			`The model could not be started. ${e instanceof Error ? e.message : String(e)}`
		);
	}
}

export async function askWebLlm(
	input: NamingInput,
	modelId: string,
	progress?: Progress
): Promise<string | null> {
	if (!hasWebGpu()) {
		throw new NamingError('This browser has no WebGPU, so it cannot run a model itself.');
	}
	if (!modelId) {
		throw new NamingError('No in-browser model is chosen. Settings → Find name.');
	}

	const answer = await ask(
		{
			type: 'name',
			model: modelId,
			system: INSTRUCTION,
			user: question(input),
			maxTokens: MAX_TOKENS
		},
		progress
	);

	return answer.type === 'done' ? cleanName(answer.text) : null;
}
