/**
 * Mirror of `AiNamer.Clean` in src/Yass.Naming/AiNamer.cs, for the model this browser asks
 * itself. Models like to add quotes, prefixes and extensions; strip them back to a bare name.
 *
 * The same precedent as $lib/template.ts: the server stays the authority, this is the copy
 * that lets the browser work without a round trip. The two are pinned to one table of cases,
 * copied out of tests/Yass.Naming.Tests/NameSuggesterTests.cs into tests/unit/naming.test.ts.
 */

/** What may not appear in a file name YASS writes, from `FileNames.Strip`. */
// eslint-disable-next-line no-control-regex
const UNSAFE = /[\u0000-\u001f<>:"/\\|?*]+/g;

const EXTENSIONS = ['.pdf', '.jpg', '.png'];

/**
 * .NET's `ToTitleCase`, near enough for names: every word gets a capital, and a word already
 * in capitals is left alone rather than turned into Acme.
 */
function titleCase(value: string): string {
	return value.replace(/\S+/g, (word) =>
		word === word.toUpperCase() ? word : word[0].toUpperCase() + word.slice(1)
	);
}

/** Drops unsafe characters rather than replacing them, and collapses the gaps they leave. */
function strip(value: string): string {
	return value.replace(UNSAFE, '').split(' ').filter(Boolean).join(' ');
}

export function cleanName(raw: string | null | undefined): string | null {
	if (!raw?.trim()) return null;

	// The first line only: an explanation the model could not resist is not part of the name.
	let name = (raw.trim().split('\n').filter(Boolean)[0] ?? '').trim();
	name = name.replace(/^["'`. ]+/, '').replace(/["'`. ]+$/, '');

	for (const extension of EXTENSIONS) {
		if (name.toLowerCase().endsWith(extension)) name = name.slice(0, -extension.length);
	}

	name = strip(name);
	if (name.length > 120) name = name.slice(0, 120).trim();

	return name.length < 2 ? null : titleCase(name).replaceAll('  ', ' ');
}
