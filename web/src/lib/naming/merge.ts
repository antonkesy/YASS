import { NameSource } from '$lib/hub/generated/Yass.Core.Naming';
import type { NameSuggestion } from '$lib/hub/generated/Yass.Core.Naming';
import type { NameSuggestionsDto } from '$lib/hub/generated/Yass.Core.Hub';

/** How many rows the popover shows before it is a list rather than a suggestion. */
export const MAX_ROWS = 5;

/** The same 0.9 the server gives its own model's answer. */
const AI_CONFIDENCE = 0.9;

export function aiSuggestion(name: string, model: string, local: boolean): NameSuggestion {
	return {
		Name: name,
		Source: NameSource.Ai,
		Reason: local ? `Written in this browser by ${model}` : `Summarised from the page by ${model}`,
		Confidence: AI_CONFIDENCE
	};
}

/**
 * Puts the browser's answer at the top of what the server already offered.
 *
 * `Local` is recomputed rather than trusted: the server says "nothing left the scanner server"
 * whenever its own provider is off, which stops being true the moment this browser posts the
 * page to someone. The popover's privacy line is only worth having if it cannot be wrong.
 */
export function merge(
	result: NameSuggestionsDto,
	extra: NameSuggestion | null,
	local: boolean
): NameSuggestionsDto {
	const rows = extra ? [extra, ...result.Suggestions] : [...result.Suggestions];

	const seen = new Set<string>();
	const unique = rows.filter((row) => {
		const key = row.Name.trim().toLowerCase();
		if (key === '' || seen.has(key)) return false;
		seen.add(key);
		return true;
	});

	return {
		...result,
		Suggestions: unique.slice(0, MAX_ROWS),
		Local: result.Local && local
	};
}

/**
 * A host the page text never really leaves: loopback, or the private ranges a home network
 * hands out. Anything else is someone else's computer, whatever its name suggests.
 */
export function isLocalEndpoint(url: string): boolean {
	let host: string;
	try {
		host = new URL(url).hostname.replace(/^\[|]$/g, '');
	} catch {
		return false;
	}

	if (host === 'localhost' || host.endsWith('.localhost') || host === '::1') return true;

	const parts = host.split('.').map(Number);
	if (parts.length !== 4 || parts.some((n) => !Number.isInteger(n) || n < 0 || n > 255)) {
		return false;
	}

	const [a, b] = parts;
	return a === 127 || a === 10 || (a === 192 && b === 168) || (a === 172 && b >= 16 && b <= 31);
}
