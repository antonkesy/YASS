/**
 * Naming from the browser (ADR-008). The server reads the page; a model the user points this
 * browser at turns what it read into a file name. Nothing here goes near the server: the key
 * lives in localStorage and the page calls the provider itself.
 */

/**
 * Who writes the name. `off` leaves the server's own suggestions as they are; `webllm` and
 * `wllama` both run a model in this browser, on the GPU and on the processor respectively
 * (ADR-010, ADR-011).
 */
export type AiProvider = 'off' | 'anthropic' | 'openai' | 'webllm' | 'wllama';

/** The two that need no key and send nothing anywhere. */
export function isInBrowser(provider: AiProvider): boolean {
	return provider === 'webllm' || provider === 'wllama';
}

/** What the model is given: what the server read, or the page itself when it could not read. */
export type NamingInput =
	| { kind: 'text'; text: string; pages: number }
	| { kind: 'image'; mediaType: string; base64: string };

/** Where a call has got to, for the popover's loading line. */
export type Phase = 'reading' | 'downloading' | 'thinking';

/**
 * A failure worth showing someone, as a finished sentence. Anything else that escapes is a
 * bug, not a provider being a provider.
 */
export class NamingError extends Error {
	constructor(message: string) {
		super(message);
		this.name = 'NamingError';
	}
}

/**
 * Why a model in this browser has nothing to work with. It reads text and cannot see, so a
 * server that reads no page text leaves it nothing — which is worth saying, in one place,
 * both where the page decides not to ask and where the ask itself is refused.
 */
export const NEEDS_PAGE_TEXT =
	'A model running in this browser can only read text, and this server reads none. ' +
	'Turn on naming.ocr in yass.yaml, or use an API key instead.';

/** A starting point per provider; every one of them is an editable field in settings. */
export const defaultModels: Record<AiProvider, string> = {
	off: '',
	anthropic: 'claude-haiku-4-5-20251001',
	openai: 'gpt-4o-mini',
	// Chosen from the package's own catalogue at runtime, so no size claim lives here.
	webllm: '',
	// A GGUF URL, chosen from `cpu-models.ts` or pasted. Nothing to default to that would
	// not be a few hundred megabytes someone did not ask for.
	wllama: ''
};

/** Where each shape goes when the user has not said otherwise, mirroring `naming.endpoint`. */
export const defaultBaseUrls: Record<AiProvider, string> = {
	off: '',
	anthropic: 'https://api.anthropic.com',
	openai: 'https://api.openai.com/v1',
	webllm: '',
	wllama: ''
};

/** Hosts worth offering as one click, all verified to answer a browser (ADR-009). */
export const knownBaseUrls: readonly { label: string; url: string }[] = [
	{ label: 'OpenAI', url: 'https://api.openai.com/v1' },
	{ label: 'OpenRouter', url: 'https://openrouter.ai/api/v1' },
	{ label: 'Groq', url: 'https://api.groq.com/openai/v1' },
	{ label: 'LM Studio', url: 'http://localhost:1234/v1' },
	{ label: 'llama.cpp', url: 'http://localhost:8080/v1' },
	{ label: 'Ollama', url: 'http://localhost:11434/v1' }
];
