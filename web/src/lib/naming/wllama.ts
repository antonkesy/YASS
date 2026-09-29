import { cleanName } from './clean';
import { INSTRUCTION, MAX_TOKENS, question } from './prompt';
import { NamingError, type NamingInput } from './types';
import type { FromWorker, ToWorker } from './wllama.protocol';
import { workerClient, type Progress } from './worker.client';

/**
 * The page's half of the model that runs on the processor (ADR-011). It knows how to ask; the
 * worker owns llama.cpp and the eight megabytes of WebAssembly behind it, and nothing here
 * imports either — so a browser that never turns this on never fetches a byte of it.
 *
 * Unlike the GPU engine there is nothing to check first: every browser has a processor.
 */

export type { Progress } from './worker.client';

const client = workerClient<ToWorker, FromWorker>(
	() => new Worker(new URL('./wllama.worker.ts', import.meta.url), { type: 'module' })
);

/**
 * Downloads a model without asking it anything, so it can be done deliberately in settings
 * rather than discovered halfway through naming a scan.
 */
export async function loadModel(url: string, progress?: Progress): Promise<void> {
	if (!url) throw new NamingError('No in-browser model is chosen. Settings → Find name.');
	try {
		await client.ask({ type: 'load', model: url }, progress);
	} catch (e) {
		// Whatever it was — a URL that is not a GGUF, a download that stopped, not enough
		// memory for the weights — it said so itself, and that is more use than a sentence
		// of ours over the top.
		throw new NamingError(
			`The model could not be started. ${e instanceof Error ? e.message : String(e)}`
		);
	}
}

export async function askWllama(
	input: NamingInput,
	url: string,
	progress?: Progress
): Promise<string | null> {
	if (!url) {
		throw new NamingError('No in-browser model is chosen. Settings → Find name.');
	}

	const answer = await client.ask(
		{
			type: 'name',
			model: url,
			system: INSTRUCTION,
			user: question(input),
			maxTokens: MAX_TOKENS
		},
		progress
	);

	return answer.type === 'done' ? cleanName(answer.text) : null;
}
