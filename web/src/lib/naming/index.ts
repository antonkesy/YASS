import type { AiKeys, AiPrefs } from '$lib/presets';
import { askAnthropic, askOpenAiCompatible } from './providers';
import { isLocalEndpoint } from './merge';
import {
	defaultBaseUrls,
	defaultModels,
	NamingError,
	type AiProvider,
	type NamingInput,
	type Phase
} from './types';

export { cleanName } from './clean';
export { fetchPageImage } from './image';
export { aiSuggestion, isLocalEndpoint, merge, MAX_ROWS } from './merge';
export { askAnthropic, askOpenAiCompatible } from './providers';
export { INSTRUCTION } from './prompt';
export * from './types';

/** What the caller asked for, and where the page text ended up going to get it. */
export type Named = { name: string; model: string; local: boolean };

export type Hooks = {
	onphase?: (phase: Phase, detail?: string) => void;
	signal?: AbortSignal;
};

/** The model this browser is set up to ask, with the blanks filled in. */
export function chosenModel(prefs: AiPrefs): string {
	return prefs.models[prefs.provider]?.trim() || defaultModels[prefs.provider];
}

/** Where that model lives. An empty setting means wherever the provider normally is. */
export function chosenBaseUrl(prefs: AiPrefs): string {
	return prefs.baseUrl.trim() || defaultBaseUrls[prefs.provider];
}

function keyFor(provider: AiProvider, keys: AiKeys): string {
	const key = provider === 'anthropic' ? keys.anthropic : keys.openai;
	if (!key.trim()) {
		throw new NamingError('No API key is set for this provider. Settings → Find name.');
	}
	return key.trim();
}

/**
 * Asks whichever model this browser is set up to ask for one file name.
 *
 * Returns null when there is nothing to ask — that is not a failure. Anything that goes wrong
 * throws a {@link NamingError} carrying a sentence fit to put on screen.
 */
export async function suggestName(
	input: NamingInput,
	prefs: AiPrefs,
	keys: AiKeys,
	hooks: Hooks = {},
	get: typeof fetch = fetch
): Promise<Named | null> {
	if (prefs.provider === 'off') return null;

	const model = chosenModel(prefs);
	const baseUrl = chosenBaseUrl(prefs);

	if (prefs.provider === 'webllm') {
		// Arrives with the package that can do it; until then settings does not offer it.
		throw new NamingError('Running a model in this browser is not set up.');
	}

	const ask = { key: keyFor(prefs.provider, keys), model, baseUrl, signal: hooks.signal };
	hooks.onphase?.('thinking');

	const name =
		prefs.provider === 'anthropic'
			? await askAnthropic(input, ask, get)
			: await askOpenAiCompatible(input, ask, get);

	return name ? { name, model, local: isLocalEndpoint(baseUrl) } : null;
}
