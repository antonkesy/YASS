/**
 * How a found name joins its words once it becomes a file name.
 *
 * The suggestions popover always shows the name as the model or the page wrote it —
 * `Quarterly Report 2026 Q3` reads at a glance and compares against its neighbours, which is
 * what a list of candidates is for. The convention is applied on the way out, where it is a
 * file name and readability has stopped being the point.
 */

export type NameStyle = 'words' | 'snake' | 'kebab';

const SEPARATORS: Record<Exclude<NameStyle, 'words'>, string> = {
	snake: '_',
	kebab: '-'
};

/** For the settings preview, so the shape of the choice is visible before a scan. */
export const SAMPLE_NAME = 'Quarterly Report 2026 Q3';

/**
 * Lower-cases and re-joins on whatever already separates the words, so a name that arrived
 * hyphenated does not end up with both kinds.
 *
 * Nothing is stripped beyond that. `cleanName` has already taken out what a file system will
 * not take, and anything more would eat the letters of every language with an accent in it —
 * `Gehaltsabrechnung Müller` has to survive as `gehaltsabrechnung-müller`.
 */
export function styleName(name: string, style: NameStyle): string {
	// Braces go whatever the style, because a name is dropped into a template and then
	// expanded — twice, on the server. A heading of "Bericht {YEAR}" would otherwise come
	// out as a date, and one of "{NAME}" would come out as itself for ever.
	const plain = name.replace(/[{}]/g, '');
	if (style === 'words') return plain;

	const separator = SEPARATORS[style];
	return (
		plain
			// Not toLocaleLowerCase: a Turkish browser must not turn "I" into a dotless ı in a
			// name a German browser would write differently.
			.toLowerCase()
			.replace(/[\s_-]+/g, separator)
			.replace(new RegExp(`^\\${separator}+|\\${separator}+$`, 'g'), '')
	);
}
