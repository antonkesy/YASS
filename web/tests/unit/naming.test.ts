import { describe, expect, it, vi } from 'vitest';
import {
	aiSuggestion,
	askAnthropic,
	askOpenAiCompatible,
	chosenBaseUrl,
	chosenModel,
	cleanName,
	CPU_MODELS,
	cpuModelLabel,
	cpuModelSizeMb,
	INSTRUCTION,
	isInBrowser,
	isLocalEndpoint,
	merge,
	NamingError,
	suggestName,
	type NamingInput
} from '$lib/naming';
import { defaultAiKeys, defaultAiPrefs, type AiPrefs } from '$lib/presets';
import { NameSource } from '$lib/hub/generated/Yass.Core.Naming';
import type { NameSuggestionsDto } from '$lib/hub/generated/Yass.Core.Hub';

const text: NamingInput = { kind: 'text', text: 'Northwind Traders\nQuarterly Report', pages: 1 };
const image: NamingInput = { kind: 'image', mediaType: 'image/webp', base64: 'AAAA' };

const ask = { key: 'sk-test', model: 'a-model', baseUrl: 'https://api.example.com/v1' };

/** A fetch that answers once, and remembers what it was asked. */
function answering(body: unknown, status = 200) {
	const calls: { url: string; init: RequestInit }[] = [];
	const get = vi.fn(async (url: string | URL | Request, init?: RequestInit) => {
		calls.push({ url: String(url), init: init ?? {} });
		return new Response(JSON.stringify(body), {
			status,
			headers: { 'content-type': 'application/json' }
		});
	}) as unknown as typeof fetch;
	return { get, calls };
}

const headers = (init: RequestInit) => init.headers as Record<string, string>;
const sent = (init: RequestInit) => JSON.parse(String(init.body)) as Record<string, unknown>;

describe('cleaning a model answer', () => {
	// The same table as AiNameCleaningTests in tests/Yass.Naming.Tests/NameSuggesterTests.cs.
	// Two implementations of one rule, pinned to one set of cases.
	it.each([
		['"Tax Return 2025.pdf"', 'Tax Return 2025'],
		['invoice acme september', 'Invoice Acme September'],
		['Report.PDF\nplus an explanation', 'Report'],
		['  bank/statement:march  ', 'Bankstatementmarch']
	])('strips quotes, extensions and extra lines: %s', (raw, expected) => {
		expect(cleanName(raw)).toBe(expected);
	});

	it.each([[''], ['   '], ['a'], [null], [undefined]])('rejects what is not a name: %s', (raw) => {
		expect(cleanName(raw)).toBeNull();
	});

	it('leaves a word that is already shouting alone', () => {
		expect(cleanName('ACME invoice')).toBe('ACME Invoice');
	});

	it('cuts a name no filesystem would take', () => {
		expect(cleanName('x'.repeat(200))?.length).toBe(120);
	});
});

describe('the Anthropic shape', () => {
	it('sends the header that makes the endpoint answer a page at all', async () => {
		const { get, calls } = answering({ content: [{ text: 'Quarterly Report' }] });

		expect(await askAnthropic(text, { ...ask, baseUrl: 'https://api.anthropic.com' }, get)).toBe(
			'Quarterly Report'
		);
		expect(calls[0].url).toBe('https://api.anthropic.com/v1/messages');
		expect(headers(calls[0].init)['anthropic-dangerous-direct-browser-access']).toBe('true');
		expect(headers(calls[0].init)['x-api-key']).toBe('sk-test');
		expect(sent(calls[0].init).system).toBe(INSTRUCTION);
	});

	it('sends the page itself when there is no text to send', async () => {
		const { get, calls } = answering({ content: [{ text: 'A Scanned Page' }] });

		await askAnthropic(image, ask, get);

		const content = sent(calls[0].init).messages as { content: { type: string }[] }[];
		expect(content[0].content[0].type).toBe('image');
	});
});

describe('the OpenAI-compatible shape', () => {
	it('posts to the base URL it is given, which carries its own /v1', async () => {
		const { get, calls } = answering({ choices: [{ message: { content: 'Invoice Acme' } }] });

		expect(await askOpenAiCompatible(text, ask, get)).toBe('Invoice Acme');
		expect(calls[0].url).toBe('https://api.example.com/v1/chat/completions');
		expect(headers(calls[0].init).authorization).toBe('Bearer sk-test');
	});

	it('sends the page as a data url when there is no text', async () => {
		const { get, calls } = answering({ choices: [{ message: { content: 'A Page' } }] });

		await askOpenAiCompatible(image, ask, get);

		const messages = sent(calls[0].init).messages as { content: { type: string }[] }[];
		expect(messages[1].content[1].type).toBe('image_url');
	});
});

describe('when a provider says no', () => {
	it.each([
		[401, /rejected the API key/],
		[403, /rejected the API key/],
		[429, /rate limiting/],
		[500, /refused the request \(500\)/]
	])('turns %i into a sentence', async (status, expected) => {
		const { get } = answering({ error: { message: 'nope' } }, status);
		await expect(askOpenAiCompatible(text, ask, get)).rejects.toThrow(expected);
	});

	it('says what a bare TypeError really means, because the browser will not', async () => {
		const get = vi.fn(() =>
			Promise.reject(new TypeError('Failed to fetch'))
		) as unknown as typeof fetch;

		await expect(askAnthropic(text, ask, get)).rejects.toThrow(/CORS/);
		await expect(askAnthropic(text, ask, get)).rejects.toBeInstanceOf(NamingError);
	});

	it('keeps a cancelled request a cancellation', async () => {
		const get = vi.fn(() =>
			Promise.reject(new DOMException('aborted', 'AbortError'))
		) as unknown as typeof fetch;

		await expect(askAnthropic(text, ask, get)).rejects.toThrow(/aborted/);
	});
});

describe('choosing who to ask', () => {
	const prefs = (over: Partial<AiPrefs> = {}): AiPrefs => ({ ...defaultAiPrefs, ...over });

	it('asks nobody when it is off, which is not a failure', async () => {
		expect(await suggestName(text, prefs(), defaultAiKeys)).toBeNull();
	});

	it('refuses to send a key it does not have', async () => {
		await expect(
			suggestName(text, prefs({ provider: 'anthropic' }), defaultAiKeys)
		).rejects.toThrow(/No API key/);
	});

	it('falls back to the provider default for the model and the host', () => {
		expect(chosenModel(prefs({ provider: 'anthropic' }))).toMatch(/^claude-/);
		expect(chosenBaseUrl(prefs({ provider: 'anthropic' }))).toBe('https://api.anthropic.com');
		expect(chosenModel(prefs({ provider: 'openai', models: { openai: ' my-model ' } }))).toBe(
			'my-model'
		);
		expect(chosenBaseUrl(prefs({ provider: 'openai', baseUrl: 'http://localhost:1234/v1' }))).toBe(
			'http://localhost:1234/v1'
		);
	});

	it('reports where the text went, and it is not always away', async () => {
		const { get } = answering({ choices: [{ message: { content: 'Bank Statement' } }] });
		const keys = { ...defaultAiKeys, openai: 'sk-test' };

		const remote = await suggestName(text, prefs({ provider: 'openai' }), keys, {}, get);
		expect(remote).toEqual({ name: 'Bank Statement', model: 'gpt-4o-mini', local: false });

		const { get: local } = answering({ choices: [{ message: { content: 'Bank Statement' } }] });
		const near = await suggestName(
			text,
			prefs({ provider: 'openai', baseUrl: 'http://localhost:11434/v1' }),
			keys,
			{},
			local
		);
		expect(near?.local).toBe(true);
	});

	it.each([
		['off', false],
		['anthropic', false],
		['openai', false],
		['webllm', true],
		['wllama', true]
	] as const)('knows whether %s runs here', (provider, here) => {
		expect(isInBrowser(provider)).toBe(here);
	});

	it.each(['webllm', 'wllama'] as const)(
		'tells %s the page image is not something it can read',
		async (provider) => {
			await expect(suggestName(image, prefs({ provider }), defaultAiKeys)).rejects.toThrow(
				/can only read text/
			);
		}
	);

	it('asks the model on the processor, and credits it by name rather than by URL', async () => {
		const chosen = CPU_MODELS[0];
		const askWllama = vi.fn(async () => 'Quarterly Report');
		vi.doMock('$lib/naming/wllama', () => ({ askWllama }));

		const named = await suggestName(
			text,
			prefs({ provider: 'wllama', models: { wllama: chosen.url } }),
			defaultAiKeys
		);

		expect(askWllama).toHaveBeenCalledWith(text, chosen.url, expect.any(Function));
		expect(named).toEqual({ name: 'Quarterly Report', model: chosen.label, local: true });
		vi.doUnmock('$lib/naming/wllama');
	});
});

describe('the models that fit on a processor', () => {
	it('pins every URL to a commit, so the size beside it cannot start lying', () => {
		for (const model of CPU_MODELS) {
			expect(model.url).toMatch(
				/^https:\/\/huggingface\.co\/[\w.-]+\/[\w.-]+\/resolve\/[0-9a-f]{40}\/[\w.-]+\.gguf$/
			);
			expect(model.bytes).toBeGreaterThan(0);
		}
	});

	it('offers them smallest first, the way the GPU engine sorts its own', () => {
		const sizes = CPU_MODELS.map((m) => m.bytes);
		expect(sizes).toEqual([...sizes].sort((a, b) => a - b));
	});

	it('names a model the popover can credit, whether it was chosen or pasted', () => {
		expect(cpuModelLabel(CPU_MODELS[0].url)).toBe(CPU_MODELS[0].label);
		expect(cpuModelLabel('https://example.com/weights/My-Own-Model-Q4_K_M.gguf')).toBe(
			'My-Own-Model-Q4_K_M'
		);
		expect(cpuModelLabel('https://example.com/weights/model.gguf?download=true')).toBe('model');
		expect(cpuModelLabel('not a url')).toBe('not a url');
	});

	it('reports a size someone can weigh against their disk', () => {
		expect(cpuModelSizeMb(144_811_360)).toBe(145);
	});
});

describe('merging the browser answer into the list', () => {
	const server = (over: Partial<NameSuggestionsDto> = {}): NameSuggestionsDto => ({
		Suggestions: [
			{ Name: 'Quarterly Report', Source: NameSource.Ocr, Reason: 'heading', Confidence: 0.8 },
			{ Name: '2026-09-29_scan', Source: NameSource.Template, Reason: 'template', Confidence: 0.5 }
		],
		CanRead: true,
		Local: true,
		...over
	});

	it('puts the model first and drops what it repeats', () => {
		const same = aiSuggestion('quarterly report', 'a-model', false);
		const merged = merge(server(), same, false);

		expect(merged.Suggestions).toHaveLength(2);
		expect(merged.Suggestions[0].Source).toBe(NameSource.Ai);
		expect(merged.Suggestions[1].Source).toBe(NameSource.Template);
	});

	it('stops claiming nothing left the machine once something has', () => {
		// The server says local because its own provider is off. This browser knows better.
		expect(merge(server(), aiSuggestion('X Y', 'm', false), false).Local).toBe(false);
		expect(merge(server(), aiSuggestion('X Y', 'm', true), true).Local).toBe(true);
		expect(merge(server({ Local: false }), null, true).Local).toBe(false);
	});

	it('caps the list at five', () => {
		const many = server({
			Suggestions: [1, 2, 3, 4, 5, 6].map((i) => ({
				Name: `Name ${i}`,
				Source: NameSource.Ocr,
				Reason: 'r',
				Confidence: 0.5
			}))
		});
		expect(merge(many, aiSuggestion('From The Model', 'm', true), true).Suggestions).toHaveLength(
			5
		);
	});

	it.each([
		['http://localhost:11434/v1', true],
		['http://127.0.0.1:1234/v1', true],
		['http://192.168.1.5:8080/v1', true],
		['http://10.0.0.2/v1', true],
		['http://172.20.0.9/v1', true],
		['http://172.32.0.9/v1', false],
		['https://api.openai.com/v1', false],
		['not a url', false]
	])('knows whether %s is somewhere else', (url, local) => {
		expect(isLocalEndpoint(url)).toBe(local);
	});
});
