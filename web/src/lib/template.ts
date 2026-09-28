/**
 * Mirror of the server's filename tokens, so the settings sheet can preview a template
 * and flag typos without a round trip. The server is still the authority: it expands the
 * template for real and leaves unknown placeholders in the name rather than dropping them.
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
	'COLOR'
] as const;

export type TemplateFacts = {
	preset: string;
	counter: number;
	dpi: number;
	color: string;
};

const pad = (n: number, width = 2) => String(n).padStart(width, '0');

export function expandTemplate(template: string, at: Date, facts: TemplateFacts): string {
	return template.replace(/\{([A-Za-z]+)\}/g, (whole, token: string) => {
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
			default:
				return whole;
		}
	});
}

/** Placeholders the server would not recognise, without braces. */
export function unknownTokens(template: string): string[] {
	const known = new Set<string>(templateTokens);
	const found = [...template.matchAll(/\{([A-Za-z]+)\}/g)].map((m) => m[1]);
	return [...new Set(found.filter((t) => !known.has(t)))];
}
