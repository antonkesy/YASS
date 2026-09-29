import { describe, expect, it } from 'vitest';
import { SAMPLE_NAME, styleName, type NameStyle } from '$lib/naming/style';

describe('styling a found name for a file name', () => {
	it.each([
		['words', 'Quarterly Report 2026 Q3'],
		['snake', 'quarterly_report_2026_q3'],
		['kebab', 'quarterly-report-2026-q3']
	] as [NameStyle, string][])('writes the sample as %s', (style, expected) => {
		expect(styleName(SAMPLE_NAME, style)).toBe(expected);
	});

	it('leaves a name alone when nothing was asked of it', () => {
		// The default, and what every browser that never touches the setting keeps doing.
		expect(styleName('ACME Invoice', 'words')).toBe('ACME Invoice');
	});

	it('re-joins on the separator asked for, whatever the name arrived with', () => {
		expect(styleName('bank-statement march', 'snake')).toBe('bank_statement_march');
		expect(styleName('bank_statement march', 'kebab')).toBe('bank-statement-march');
		// Runs collapse rather than doubling up.
		expect(styleName('a - b', 'kebab')).toBe('a-b');
	});

	it('keeps the letters of languages that have accents in them', () => {
		// Stripping to ASCII would turn a payslip into gehaltsabrechnung-mller.
		expect(styleName('Gehaltsabrechnung Müller', 'kebab')).toBe('gehaltsabrechnung-müller');
		expect(styleName('Facture Été', 'snake')).toBe('facture_été');
	});

	it.each(['words', 'snake', 'kebab'] as NameStyle[])(
		'takes braces out whatever the style (%s)',
		(style) => {
			// A name goes into a template and is then expanded, twice, on the server. A brace
			// that survived would come back as a date, or as the token itself for ever.
			expect(styleName('Bericht {YEAR}', style)).not.toContain('{');
			expect(styleName('{NAME}', style)).not.toContain('}');
		}
	);

	it('does not leave a name starting or ending in its separator', () => {
		expect(styleName(' Invoice ', 'kebab')).toBe('invoice');
		expect(styleName('-Invoice-', 'snake')).toBe('invoice');
	});
});
