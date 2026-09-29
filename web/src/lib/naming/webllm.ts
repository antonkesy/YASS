import { cleanName } from './clean';
import { INSTRUCTION, MAX_TOKENS, question } from './prompt';
import { NamingError, type NamingInput } from './types';
import type { FromWorker, LocalModel, ToWorker } from './webllm.protocol';
import { workerClient, type Progress } from './worker.client';

/**
 * The page's half of the model that runs on the GPU (ADR-010). It knows how to ask; the worker
 * owns the package that answers, and nothing here imports it — that is what keeps six megabytes
 * of WebGPU runtime off the first visit of everyone who never turns this on.
 */

export type { Progress } from './worker.client';
export type { LocalModel };

/** Whether this browser can run a model on its GPU at all. */
export function hasWebGpu(): boolean {
	return typeof navigator !== 'undefined' && navigator.gpu != null;
}

const client = workerClient<ToWorker, FromWorker>(
	() => new Worker(new URL('./webllm.worker.ts', import.meta.url), { type: 'module' })
);

/** The models the package ships, smallest first, with its own size figures rather than ours. */
export async function localModels(): Promise<LocalModel[]> {
	if (!hasWebGpu()) return [];
	const answer = await client.ask({ type: 'models' });
	return answer.type === 'done' ? (answer.models ?? []) : [];
}

/**
 * Downloads a model without asking it anything, so it can be done deliberately in settings
 * rather than discovered halfway through naming a scan.
 */
export async function loadModel(modelId: string, progress?: Progress): Promise<void> {
	if (!hasWebGpu()) throw new NamingError('This browser has no WebGPU.');
	try {
		await client.ask({ type: 'load', model: modelId }, progress);
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

	const answer = await client.ask(
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
