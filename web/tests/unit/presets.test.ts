import { describe, expect, it } from 'vitest';
import { ColorMode, PageSize, ScanSource } from '$lib/hub/generated/Yass.Core.Domain';
import {
	activePreset,
	defaultExportPrefs,
	defaultPresets,
	matchesPreset,
	rememberFolder
} from '$lib/presets';

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

describe('preset matching against planned options', () => {
	// The stage compares presets to what the next scan will do (defaults + overrides),
	// so a browser that has never changed anything still lights up the matching preset.
	it('matches the server defaults when nothing is overridden', () => {
		const planned = {
			Source: ScanSource.Feeder,
			Color: ColorMode.Color,
			Dpi: 300,
			PageSize: PageSize.A4,
			BlankPageDetection: true,
			Editable: ['source', 'dpi']
		};
		expect(activePreset(defaultPresets, planned)?.id).toBe('document');
	});

	it('falls back to no preset once an override diverges', () => {
		const planned = {
			Source: ScanSource.Duplex,
			Color: ColorMode.Color,
			Dpi: 600,
			PageSize: PageSize.A4
		};
		expect(activePreset(defaultPresets, planned)).toBeNull();
	});
});

describe('recent export folders', () => {
	const prefs = { ...defaultExportPrefs };

	it('keeps the newest first, without repeats, capped at four', () => {
		let p = prefs;
		for (const dir of ['/a', '/b', '/c', '/d', '/e']) p = rememberFolder(p, dir);
		expect(p.recentFolders).toEqual(['/e', '/d', '/c', '/b']);

		p = rememberFolder(p, '/c');
		expect(p.recentFolders).toEqual(['/c', '/e', '/d', '/b']);
	});

	it('normalises the path and ignores an empty one', () => {
		expect(rememberFolder(prefs, ' /data/consume/ ').recentFolders).toEqual(['/data/consume']);
		expect(rememberFolder(prefs, '   ')).toBe(prefs);
	});
});
