import { cleanName } from './clean';
import { INSTRUCTION, MAX_TOKENS, question } from './prompt';
import { NamingError, type NamingInput } from './types';

/**
 * The two request shapes a browser can reach (ADR-009): Anthropic's messages endpoint, and
 * the OpenAI-compatible chat completions shape that every other host speaks. Both take an
 * injected `fetch` so the tests never touch a network.
 */

export type Ask = {
	key: string;
	model: string;
	baseUrl: string;
	signal?: AbortSignal;
};

/**
 * A cross-origin refusal and a dead network are the same bare `TypeError` in a browser — no
 * status, no headers, nothing to tell them apart — so the message has to name both.
 */
async function send(url: string, init: RequestInit, get: typeof fetch): Promise<Response> {
	try {
		return await get(url, init);
	} catch (e) {
		if (e instanceof DOMException && e.name === 'AbortError') throw e;
		const host = safeHost(url);
		throw new NamingError(
			`The browser could not reach ${host}. Either it is unreachable, or it does not allow ` +
				'requests from web pages (CORS).'
		);
	}
}

function safeHost(url: string): string {
	try {
		return new URL(url).host;
	} catch {
		return url;
	}
}

/** Turns a refusal into a sentence, mirroring `AiNamer.Explain` on the server. */
async function refuse(response: Response, who: string): Promise<never> {
	let detail = '';
	try {
		const body = (await response.json()) as { error?: { message?: string } };
		detail = body.error?.message ?? '';
	} catch {
		// A provider that cannot even produce an error body has said enough with its status.
	}

	if (response.status === 401 || response.status === 403) {
		throw new NamingError(`${who} rejected the API key.`);
	}
	if (response.status === 429) {
		throw new NamingError(`${who} is rate limiting; try again shortly.`);
	}
	throw new NamingError(
		`${who} refused the request (${response.status}).${detail ? ' ' + detail : ''}`
	);
}

export async function askAnthropic(
	input: NamingInput,
	ask: Ask,
	get: typeof fetch = fetch
): Promise<string | null> {
	const response = await send(
		`${ask.baseUrl.replace(/\/+$/, '')}/v1/messages`,
		{
			method: 'POST',
			headers: {
				'content-type': 'application/json',
				'x-api-key': ask.key,
				'anthropic-version': '2023-06-01',
				// What the official SDK sends for dangerouslyAllowBrowser, and the only thing
				// that makes this endpoint answer a page at all (ADR-009).
				'anthropic-dangerous-direct-browser-access': 'true'
			},
			body: JSON.stringify({
				model: ask.model,
				max_tokens: MAX_TOKENS,
				system: INSTRUCTION,
				messages: [{ role: 'user', content: [{ type: 'text', text: question(input) }] }]
			}),
			signal: ask.signal
		},
		get
	);

	if (!response.ok) await refuse(response, 'Anthropic');
	const body = (await response.json()) as { content?: { text?: string }[] };
	return cleanName(body.content?.[0]?.text);
}

export async function askOpenAiCompatible(
	input: NamingInput,
	ask: Ask,
	get: typeof fetch = fetch
): Promise<string | null> {
	// The base URL carries its own /v1, which is how every one of these hosts documents itself.
	const response = await send(
		`${ask.baseUrl.replace(/\/+$/, '')}/chat/completions`,
		{
			method: 'POST',
			headers: {
				'content-type': 'application/json',
				authorization: `Bearer ${ask.key}`
			},
			body: JSON.stringify({
				model: ask.model,
				max_tokens: MAX_TOKENS,
				messages: [
					{ role: 'system', content: INSTRUCTION },
					{ role: 'user', content: question(input) }
				]
			}),
			signal: ask.signal
		},
		get
	);

	if (!response.ok) await refuse(response, safeHost(ask.baseUrl));
	const body = (await response.json()) as { choices?: { message?: { content?: string } }[] };
	return cleanName(body.choices?.[0]?.message?.content);
}
