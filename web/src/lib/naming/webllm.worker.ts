import { CreateMLCEngine, ModelType, prebuiltAppConfig, type MLCEngine } from '@mlc-ai/web-llm';
import type { FromWorker, ToWorker } from './webllm.protocol';

/**
 * The model lives here and nowhere else. Not only because decoding on the main thread would
 * freeze the review screen — the package is six megabytes of WebGPU runtime, and keeping every
 * reference to it inside this worker is what stops the page paying for it (ADR-010).
 *
 * That is why this talks in a protocol of its own rather than through WebWorkerMLCEngine: the
 * client half of that lives on the page, and would bring the whole package with it.
 */

let engine: MLCEngine | null = null;
let loaded = '';

async function engineFor(modelId: string): Promise<MLCEngine> {
	if (engine && loaded === modelId) return engine;

	engine = await CreateMLCEngine(modelId, {
		initProgressCallback: (report) =>
			post({
				type: 'progress',
				percent: Math.round(report.progress * 100),
				text: report.text
			})
	});
	loaded = modelId;
	return engine;
}

function post(message: FromWorker) {
	self.postMessage(message);
}

async function handle(request: ToWorker): Promise<FromWorker> {
	switch (request.type) {
		case 'models':
			return {
				type: 'done',
				id: request.id,
				models: prebuiltAppConfig.model_list
					// Chat models only: the catalogue also ships embedding models, which have no
					// answer to give, and vision models, which a page this size does not need.
					.filter((m) => (m.model_type ?? ModelType.LLM) === ModelType.LLM)
					.filter((m) => (m.vram_required_MB ?? 0) > 0)
					.map((m) => ({ id: m.model_id, sizeMb: Math.round(m.vram_required_MB ?? 0) }))
					.sort((a, b) => a.sizeMb - b.sizeMb)
			};

		case 'load':
			await engineFor(request.model);
			return { type: 'done', id: request.id };

		case 'name': {
			const ready = await engineFor(request.model);
			// The same OpenAI shape the remote providers answer in.
			const reply = await ready.chat.completions.create({
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
