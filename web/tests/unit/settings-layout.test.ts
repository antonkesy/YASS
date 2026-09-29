import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * The settings sheet hides its scanner controls behind `scanner.ui_editable`, and everything
 * that is this browser's own — where a document goes, what it is called, how a found name is
 * written, which model to ask — has to stay out of that branch. A server that lets the UI
 * change no scan setting still has to let you name what comes off it.
 *
 * This is asserted against the source rather than a rendered page because the branch needs a
 * server started with `ui_editable: []`, and that cannot be expressed through the environment
 * variables the e2e server takes. A comment saying the same thing was already there, and the
 * export section ended up inside the branch anyway, so this is the cheaper guard that would
 * actually have caught it.
 */
const page = readFileSync(resolve('src/routes/settings/+page.svelte'), 'utf8');

describe('what the settings sheet shows when nothing is editable', () => {
	const nothingEditable = page.indexOf('data-testid="nothing-editable"');
	// The `{/if}` that closes the whole error / loading / nothing-editable / else chain.
	const branchEnds = page.indexOf('\n\t\t{/if}\n', nothingEditable);

	it('has a branch that hides the scan controls, and a place after it', () => {
		expect(nothingEditable).toBeGreaterThan(-1);
		expect(branchEnds).toBeGreaterThan(nothingEditable);
	});

	it.each(['export-section', 'naming-section'])(
		'keeps %s reachable on a server that lets the UI change nothing',
		(section) => {
			const at = page.indexOf(`data-testid="${section}"`);
			expect(at).toBeGreaterThan(-1);
			expect(at).toBeGreaterThan(branchEnds);
		}
	);

	it('still keeps the scan controls inside it', () => {
		// The other half of the rule: a preset is a bundle of scan overrides and has nothing
		// to offer when none of them can be set.
		const presets = page.indexOf('data-testid="presets"');
		expect(presets).toBeGreaterThan(nothingEditable);
		expect(presets).toBeLessThan(branchEnds);
	});
});
