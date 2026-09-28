import { describe, expect, it } from 'vitest';
import { expandTemplate, templateTokens, unknownTokens } from '$lib/template';

const at = new Date(2026, 8, 24, 15, 4, 5); // 24 September 2026, 15:04:05 local
const facts = { preset: 'Document', counter: 42, dpi: 300, color: 'Color' };

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

	it('leaves an unknown token in place rather than dropping it', () => {
		expect(expandTemplate('scan-{NOPE}', at, facts)).toBe('scan-{NOPE}');
		expect(unknownTokens('scan-{NOPE}-{ALSONO}-{YEAR}')).toEqual(['NOPE', 'ALSONO']);
		expect(unknownTokens('{YEAR}-{MONTH}')).toEqual([]);
	});

	it('offers the twelve tokens the server knows', () => {
		expect(templateTokens).toHaveLength(12);
	});
});
