import { Wllama } from '@wllama/wllama';
import wasmUrl from '@wllama/wllama/esm/wasm/wllama.wasm?url';
import type { FromWorker, ToWorker } from './wllama.protocol';

/**
 * llama.cpp, compiled to WebAssembly, decoding on the processor (ADR-011). It lives here and
 * nowhere else for the same two reasons the GPU worker does: the main thread must not stall on
 * the one screen where someone is flipping through pages, and the package must never reach the
 * page's own bundle. The wasm arrives as a URL the worker fetches, not as a page import.
 *
 * One thread, because multi-threaded wasm needs SharedArrayBuffer, which needs cross-origin
 * isolation, which would change how the host serves every response. wllama falls back on its
 * own; ADR-011 records what that costs.
 */

let wllama: Wllama | null = null;
let loaded = '';

/**
 * Enough context for `MAX_TEXT` characters of page text plus the instruction, and no more:
 * the KV cache for a context this size is the other half of what the weights ask of RAM.
 */
const N_CTX = 2048;

async function ready(url: string): Promise<Wllama> {
	if (wllama && loaded === url) return wllama;

	// A different model means a different set of weights; the old one has nothing to offer.
	if (wllama) await wllama.exit();

	const next = new Wllama({ default: wasmUrl });
	await next.loadModelFromUrl(url, {
		n_ctx: N_CTX,
		progressCallback: ({ loaded: got, total }) =>
			post({ type: 'progress', percent: total > 0 ? Math.round((got / total) * 100) : 0 }),
		// The download is hundreds of megabytes once, then it is in the browser's cache.
		useCache: true
	});

	wllama = next;
	loaded = url;
	return next;
}

function post(message: FromWorker) {
	self.postMessage(message);
}

async function handle(request: ToWorker): Promise<FromWorker> {
	switch (request.type) {
		case 'load':
			await ready(request.model);
			return { type: 'done', id: request.id };

		case 'name': {
			const model = await ready(request.model);
			// The same OpenAI shape the remote providers and the GPU engine both answer in.
			const reply = await model.createChatCompletion({
				messages: [
					{ role: 'system', content: request.system },
					{ role: 'user', content: request.user }
				],
				max_tokens: request.maxTokens
			});
			return { type: 'done', id: request.id, text: reply.choices[0]?.message?.content ?? null };
		}
	}
}

self.onmessage = async (event: MessageEvent<ToWorker>) => {
	try {
		post(await handle(event.data));
	} catch (e) {
		post({
			type: 'failed',
			id: event.data.id,
			error: e instanceof Error ? e.message : String(e)
		});
	}
};
