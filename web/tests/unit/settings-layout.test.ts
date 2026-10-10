import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

// Browser-local settings must stay outside the branch hidden on a GetEffectiveOptions error.
// Asserted on source because the e2e server cannot be made to fail that call.
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
		expect(at('metadata-section')).toBeGreaterThan(at('naming-section'));
		expect(at('editing-section')).toBeGreaterThan(at('metadata-section'));
	});

	it.each(['export-section', 'naming-section', 'metadata-section', 'editing-section'])(
		'keeps %s reachable while the scanner section is loading or has failed',
		(section) => {
			expect(at(section)).toBeGreaterThan(scannerEnds);
		}
	);

	it('keeps the scan controls, and the presets that bundle them, inside the branch', () => {
		const loading = page.indexOf('{:else if !effective}', scanner);
		const presets = page.indexOf('<PresetManager');
		const form = page.indexOf('<ScanDefaultsForm');

		expect(loading).toBeGreaterThan(scanner);
		expect(loading).toBeLessThan(scannerEnds);
		expect(presets).toBeGreaterThan(loading);
		expect(presets).toBeLessThan(scannerEnds);
		expect(form).toBeGreaterThan(presets);
		expect(form).toBeLessThan(scannerEnds);
	});
});
