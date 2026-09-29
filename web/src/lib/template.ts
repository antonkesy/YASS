/**
 * Mirror of the server's filename tokens, so the settings sheet can preview a template
 * and flag typos without a round trip. The server is still the authority: it expands the
 * template for real and leaves unknown placeholders in the name rather than dropping them.
 *
 * `NAME` is the one exception, and it is browser-only. It stands for the name Find name
 * came back with, which only this browser knows — the model may have run here. The server
 * has never heard of it, and leaves an unknown placeholder written out as it found it, so
 * every string that leaves for the server goes through {@link resolveName} first.
 */
export const templateTokens = [
	'YEAR',
	'MONTH',
	'MONTHNAME',
	'DAY',
	'WEEKDAY',
	'HOUR',
	'MINUTE',
	'SECOND',
	'PRESET',
	'COUNTER',
	'DPI',
	'COLOR',
	'NAME'
] as const;

export type TemplateFacts = {
	preset: string;
	counter: number;
	dpi: number;
	color: string;
	/** What Find name came back with. Empty when nothing has been found for this scan. */
	name: string;
};

/** What counts as a separator between parts of a file name, for tidying an empty `{NAME}`. */
const SEPARATOR = /[-_. ]/;

/**
 * Substitutes `{NAME}` and nothing else, leaving every other token for whoever expands next
 * — which is usually the server. This is what keeps a browser-only token from ever reaching
 * it.
 *
 * An empty name takes one adjacent separator with it, so a template of
 * `{YEAR}-{MONTH}-{DAY}_{NAME}` with nothing found reads `2026-09-29` rather than
 * `2026-09-29_`. Whatever is left is trimmed of separators at both ends.
 */
export function resolveName(template: string, name: string): string {
	// The function form, because a string replacement treats `$&` and `` $` `` as references
	// into the match — a found name of "100$ Rechnung" would otherwise rebuild the token it
	// was replacing, and put `{NAME}` back into the file name.
	if (name) return template.replaceAll('{NAME}', () => name);

	const emptied = template.replace(new RegExp(`${SEPARATOR.source}?\\{NAME\\}`, 'g'), '');
	return emptied.replace(new RegExp(`^${SEPARATOR.source}+|${SEPARATOR.source}+$`, 'g'), '');
}

const pad = (n: number, width = 2) => String(n).padStart(width, '0');

export function expandTemplate(template: string, at: Date, facts: TemplateFacts): string {
	// Every other token first, and the found name last. A name is page text, not a template:
	// filling it in before this pass would let a heading of "Bericht {YEAR}" come back as a
	// date. `{NAME}` has no case below, so it falls through untouched until `resolveName`.
	const expanded = template.replace(/\{([A-Za-z]+)\}/g, (whole, token: string) => {
		switch (token) {
			case 'YEAR':
				return String(at.getFullYear());
			case 'MONTH':
				return pad(at.getMonth() + 1);
			case 'MONTHNAME':
				return at.toLocaleDateString(undefined, { month: 'long' });
			case 'DAY':
				return pad(at.getDate());
			case 'WEEKDAY':
				return at.toLocaleDateString(undefined, { weekday: 'long' });
			case 'HOUR':
				return pad(at.getHours());
			case 'MINUTE':
				return pad(at.getMinutes());
			case 'SECOND':
				return pad(at.getSeconds());
			case 'PRESET':
				return facts.preset;
			case 'COUNTER':
				return String(facts.counter);
			case 'DPI':
				return String(facts.dpi);
			case 'COLOR':
				return facts.color;
			// No `NAME` case: the pre-pass above has already filled or removed every one of
			// them, and a second chance here would expand a token the found name contained.
			default:
				return whole;
		}
	});

	return resolveName(expanded, facts.name);
}

/** Placeholders the server would not recognise, without braces. */
export function unknownTokens(template: string): string[] {
	const known = new Set<string>(templateTokens);
	const found = [...template.matchAll(/\{([A-Za-z]+)\}/g)].map((m) => m[1]);
	return [...new Set(found.filter((t) => !known.has(t)))];
}
