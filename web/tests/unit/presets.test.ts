import { describe, expect, it } from 'vitest';
import { ColorMode, ScanSource } from '$lib/hub/generated/Yass.Core.Domain';
import { activePreset, defaultPresets, matchesPreset } from '$lib/presets';

describe('presets', () => {
	const document = defaultPresets[0];

	it('matches when every pinned key agrees, extra overrides aside', () => {
		expect(matchesPreset(document, { ...document.settings })).toBe(true);
		expect(matchesPreset(document, { ...document.settings, Brightness: 5 })).toBe(true);
		expect(matchesPreset(document, { ...document.settings, Dpi: 600 })).toBe(false);
		expect(matchesPreset(document, {})).toBe(false);
	});

	it('reports no active preset once the settings drift', () => {
		expect(activePreset(defaultPresets, { ...document.settings })?.id).toBe('document');
		expect(
			activePreset(defaultPresets, { Source: ScanSource.Feeder, Color: ColorMode.Bw, Dpi: 1200 })
		).toBeNull();
	});
});
