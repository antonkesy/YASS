import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * The settings sheet hides its scanner controls behind `scanner.ui_editable`, and everything
 * that is this browser's own — where a document goes, what it is called — has to stay out of
 * that branch. A server that lets the UI
 * change no scan setting still has to let you name what comes off it.
 *
 * This is asserted against the source rather than a rendered page because the branch needs a
 * server started with `ui_editable: []`, and that cannot be expressed through the environment
 * variables the e2e server takes. A comment saying the same thing was already there, and the
 * export section ended up inside the branch anyway, so this is the cheaper guard that would
 * actually have caught it.
 */
const page = readFileSync(resolve('src/routes/settings/+page.svelte'), 'utf8');

const at = (testid: string) => page.indexOf(`testid="${testid}"`);

describe('how the settings sheet is laid out', () => {
	const scanner = at('scanner-section');
	// Sections do not nest, so the first close after the scanner heading is its own.
	const scannerEnds = page.indexOf('</SettingsSection>', scanner);

	it('is a run of named sections, in the order they are worth reading', () => {
		expect(scanner).toBeGreaterThan(-1);
		expect(at('export-section')).toBeGreaterThan(scanner);
		expect(at('naming-section')).toBeGreaterThan(at('export-section'));
		expect(at('editing-section')).toBeGreaterThan(at('naming-section'));
	});

	it.each(['export-section', 'naming-section', 'editing-section'])(
		'keeps %s reachable on a server that lets the UI change nothing',
		(section) => {
			expect(at(section)).toBeGreaterThan(scannerEnds);
		}
	);

	it('keeps the scan controls, and the presets that bundle them, inside the branch', () => {
		// A preset is a bundle of scan overrides and has nothing to offer when none of them
		// can be set, so it belongs in the section that the branch hides.
		const nothingEditable = page.indexOf('data-testid="nothing-editable"');
		const presets = page.indexOf('data-testid="presets"');

		expect(nothingEditable).toBeGreaterThan(scanner);
		expect(nothingEditable).toBeLessThan(scannerEnds);
		expect(presets).toBeGreaterThan(nothingEditable);
		expect(presets).toBeLessThan(scannerEnds);
	});
});
