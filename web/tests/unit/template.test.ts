import { describe, expect, it } from 'vitest';
import { expandTemplate, resolveName, templateTokens, unknownTokens } from '$lib/template';

const at = new Date(2026, 8, 24, 15, 4, 5); // 24 September 2026, 15:04:05 local
const facts = { preset: 'Document', counter: 42, dpi: 300, color: 'Color', name: '' };

describe('expandTemplate', () => {
	it('fills date and time tokens, zero-padded', () => {
		expect(expandTemplate('{YEAR}-{MONTH}-{DAY}_{HOUR}{MINUTE}{SECOND}', at, facts)).toBe(
			'2026-09-24_150405'
		);
	});

	it('fills the scan tokens', () => {
		expect(expandTemplate('{PRESET}-{COUNTER}-{DPI}-{COLOR}', at, facts)).toBe(
			'Document-42-300-Color'
		);
	});

	it('drops the found name in where it was asked for', () => {
		expect(
			expandTemplate('{YEAR}-{MONTH}-{DAY}_{NAME}', at, { ...facts, name: 'quarterly-report' })
		).toBe('2026-09-24_quarterly-report');
	});

	it('does not read a token that came in with the found name', () => {
		// The name is filled first and then left alone; a heading is page text, not a template.
		expect(expandTemplate('{NAME}', at, { ...facts, name: 'Bericht {YEAR}' })).toBe(
			'Bericht {YEAR}'
		);
	});

	it('leaves an unknown token in place rather than dropping it', () => {
		expect(expandTemplate('scan-{NOPE}', at, facts)).toBe('scan-{NOPE}');
		expect(unknownTokens('scan-{NOPE}-{ALSONO}-{YEAR}')).toEqual(['NOPE', 'ALSONO']);
		expect(unknownTokens('{YEAR}-{MONTH}')).toEqual([]);
	});

	it('offers the thirteen tokens the editor puts on its chips', () => {
		// Twelve the server knows, plus {NAME}, which only this browser can fill.
		expect(templateTokens).toHaveLength(13);
		expect(templateTokens).toContain('NAME');
	});
});

describe('resolveName', () => {
	it('substitutes only {NAME}, leaving the rest for whoever expands next', () => {
		expect(resolveName('{YEAR}-{MONTH}_{NAME}', 'tax-return')).toBe('{YEAR}-{MONTH}_tax-return');
	});

	it('fills every {NAME} in a template, not just the first', () => {
		expect(resolveName('{NAME}/{NAME}', 'x')).toBe('x/x');
	});

	it.each([
		// A found name and its separator go together, so nothing is left dangling.
		['{YEAR}-{MONTH}-{DAY}_{NAME}', '{YEAR}-{MONTH}-{DAY}'],
		['{YEAR}_{NAME}_{MONTH}', '{YEAR}_{MONTH}'],
		['{NAME}_{YEAR}', '{YEAR}'],
		['scan_{NAME}', 'scan'],
		// Nothing but the name: the empty string, which every caller already reads as
		// "no template" and falls back from.
		['{NAME}', ''],
		// Untouched when there is nothing to resolve.
		['{YEAR}-{MONTH}', '{YEAR}-{MONTH}']
	])('tidies %s away to %s when nothing was found', (template, expected) => {
		expect(resolveName(template, '')).toBe(expected);
	});

	it.each(['100$ Rechnung', 'a$&b', 'x$`y', "$'z", '$1'])(
		'puts %s in verbatim, dollars and all',
		(name) => {
			// A string replacement reads `$&` as the match and `` $` `` as what came before it,
			// so a name with a dollar in it would rebuild the very token it replaced.
			expect(resolveName('{YEAR}_{NAME}', name)).toBe(`{YEAR}_${name}`);
		}
	);

	it('never leaves the browser-only token behind for the server to write out', () => {
		// The one failure that would reach a real file name, so it is asserted directly.
		for (const template of ['{NAME}', 'a_{NAME}', '{NAME}_b', '{YEAR}.{NAME}.{MONTH}']) {
			expect(resolveName(template, '')).not.toContain('{NAME}');
			expect(resolveName(template, 'found')).not.toContain('{NAME}');
		}
	});
});
