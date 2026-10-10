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

/**
 * The older placeholders the server still expands for config files written before the tokens
 * above: `{date}` and `{time}`, each maybe with a .NET format after a colon, and `{session}`. Not
 * offered as chips, but not typos either.
 */
const legacyTokens = ['date', 'time', 'session'];

/** A placeholder as the server's FilenameTemplate reads it: a name, and maybe `:format`. */
const placeholder = () => /\{([A-Za-z]+)(?::([^}]*))?\}/g;

const pad = (n: number, width = 2) => String(n).padStart(width, '0');

/**
 * Names of months and days in English whatever the browser's language: the server writes them
 * with the invariant culture, and the preview has to promise the name it will actually get.
 */
const NAMES_LOCALE = 'en-US';

export function expandTemplate(template: string, at: Date, facts: TemplateFacts): string {
	return template.replace(placeholder(), (whole, token: string, format?: string) => {
		switch (token) {
			// The server's defaults; a custom .NET format is left for the server to read.
			case 'date':
				return format
					? whole
					: `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}`;
			case 'time':
				return format ? whole : `${pad(at.getHours())}${pad(at.getMinutes())}`;
			case 'YEAR':
				return String(at.getFullYear());
			case 'MONTH':
				return pad(at.getMonth() + 1);
			case 'MONTHNAME':
				return at.toLocaleDateString(NAMES_LOCALE, { month: 'long' });
			case 'DAY':
				return pad(at.getDate());
			case 'WEEKDAY':
				return at.toLocaleDateString(NAMES_LOCALE, { weekday: 'long' });
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
	const known = new Set<string>([...templateTokens, ...legacyTokens]);
	const found = [...template.matchAll(placeholder())].map((m) => m[1]);
	return [...new Set(found.filter((t) => !known.has(t)))];
}
