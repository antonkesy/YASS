import type { NamingInput } from './types';

/**
 * Word for word the `Instruction` in src/Yass.Naming/AiNamer.cs. Two models asked the same
 * question should be asked it the same way, whichever side of the wire they are on.
 */
export const INSTRUCTION =
	'You name scanned documents. Reply with ONLY a file name: 2 to 6 words, no extension, ' +
	"no path, no quotes, no explanation. Prefer the document's own title, its issuer and its " +
	'date if they appear. Use spaces, not underscores.';

/** Enough for a title and a letterhead; more is cost without accuracy. Also `AiNamer`'s number. */
export const MAX_TEXT = 4000;

/** How many tokens a file name can possibly need. */
export const MAX_TOKENS = 64;

export function excerpt(text: string): string {
	return text.length > MAX_TEXT ? text.slice(0, MAX_TEXT) : text;
}

/** The user turn, minus the part whose shape differs per provider. */
export function question(input: NamingInput): string {
	return input.kind === 'text' ? 'Document:\n' + excerpt(input.text) : 'Document:';
}
