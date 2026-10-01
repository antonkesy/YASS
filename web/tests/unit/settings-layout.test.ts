import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * The settings sheet's scanner controls wait for the server's defaults and vanish behind an
 * error, and everything that is this browser's own — where a document goes, what it is called —
 * has to stay out of that branch. A server that cannot say how it scans still has to let you
 * name what comes off it.
 *
 * This is asserted against the source rather than a rendered page because the branch needs a
 * server that fails GetEffectiveOptions, which the e2e server cannot be told to do. A comment
 * saying the same thing was already there, and the export section ended up inside the branch
 * anyway, so this is the cheaper guard that would actually have caught it.
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
		'keeps %s reachable while the scanner section is loading or has failed',
		(section) => {
			expect(at(section)).toBeGreaterThan(scannerEnds);
		}
	);

	it('keeps the scan controls, and the presets that bundle them, inside the branch', () => {
		// A preset is a bundle of scan overrides and has nothing to offer until the defaults
		// they sit on are known, so it belongs in the section that the branch hides.
		const loading = page.indexOf('{:else if !effective}', scanner);
		const presets = page.indexOf('data-testid="presets"');
		const form = page.indexOf('data-testid="settings-form"');

		expect(loading).toBeGreaterThan(scanner);
		expect(loading).toBeLessThan(scannerEnds);
		expect(presets).toBeGreaterThan(loading);
		expect(presets).toBeLessThan(scannerEnds);
		expect(form).toBeGreaterThan(presets);
		expect(form).toBeLessThan(scannerEnds);
	});
});
