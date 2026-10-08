import { describe, expect, it } from 'vitest';
import { ColorMode, PageSize, ScanSource } from '$lib/hub/generated/Yass.Core.Domain';
import {
	EXPORT_KEY,
	PRESETS_KEY,
	activePreset,
	applyPreset,
	createExportPrefs,
	createPresets,
	defaultExportPrefs,
	documentMetadata,
	defaultPresets,
	matchesPreset,
	presetsFor,
	type Preset
} from '$lib/presets';
import { get } from 'svelte/store';

describe('presets', () => {
	const document = defaultPresets[0];

	it('matches when every pinned key agrees, extra overrides aside', () => {
		expect(matchesPreset(document, { ...document.settings })).toBe(true);
		expect(matchesPreset(document, { ...document.settings, Brightness: 5 })).toBe(true);
		expect(matchesPreset(document, { ...document.settings, Dpi: 600 })).toBe(false);
		expect(matchesPreset(document, {})).toBe(false);
	});

	it('matches nothing at all when it pins nothing', () => {
		// Saving with no overrides but the scanner leaves this behind, and every() over an
		// empty list would otherwise say yes to any settings whatsoever.
		const empty: Preset = { id: 'empty', name: 'Empty', icon: 'star', settings: {} };
		expect(matchesPreset(empty, { Dpi: 600 })).toBe(false);
		expect(activePreset([empty], { Dpi: 600 })).toBeNull();
	});

	it('reports no active preset once the settings drift', () => {
		expect(activePreset(defaultPresets, { ...document.settings })?.id).toBe('document');
		expect(
			activePreset(defaultPresets, { Source: ScanSource.Feeder, Color: ColorMode.Bw, Dpi: 1200 })
		).toBeNull();
	});
});

describe('presets scoped to a scanner', () => {
	const scoped = (id: string, device?: string): Preset => ({
		id,
		name: id,
		icon: 'star',
		settings: { Dpi: 300 },
		...(device ? { device } : {})
	});
	const all = [scoped('everywhere'), scoped('epson', 'epson:1'), scoped('canon', 'canon:2')];

	it('offers a scanner its own presets plus the ones that belong to none', () => {
		expect(presetsFor(all, 'epson:1').map((p) => p.id)).toEqual(['everywhere', 'epson']);
		expect(presetsFor(all, 'canon:2').map((p) => p.id)).toEqual(['everywhere', 'canon']);
	});

	it('leaves only the unscoped ones when no scanner is chosen', () => {
		expect(presetsFor(all, undefined).map((p) => p.id)).toEqual(['everywhere']);
		// Which is every preset a browser that predates this feature has, and all the built-ins.
		expect(presetsFor(defaultPresets, undefined)).toHaveLength(defaultPresets.length);
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
			BlankPageDetection: true
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

describe('picking between presets that overlap', () => {
	const document = defaultPresets[0];
	// Saved while Document was lit: Document's keys and everything else the scan would do.
	const office: Preset = {
		id: 'office',
		name: 'Office',
		icon: 'briefcase',
		settings: { ...document.settings, Brightness: 0, Contrast: 0, BlankPageDetection: true }
	};
	const all = [...defaultPresets, office];
	const planned = { ...office.settings };

	it('lights the preset that was picked, not the first one that happens to match', () => {
		expect(activePreset(all, planned, 'office')?.id).toBe('office');
		expect(activePreset(all, planned, 'document')?.id).toBe('document');
	});

	it('prefers the most specific match when nothing was picked', () => {
		expect(activePreset(all, planned)?.id).toBe('office');
	});

	it('ignores a picked preset that no longer matches', () => {
		expect(activePreset(all, { ...planned, Dpi: 600 }, 'office')).toBeNull();
		expect(activePreset(all, { ...planned, Brightness: 5 }, 'office')?.id).toBe('document');
	});

	it('replaces the overrides rather than leaving the last preset behind', () => {
		const receipt = defaultPresets[2];
		const photo = defaultPresets[3];
		const after = applyPreset(photo, applyPreset(receipt, { Device: 'epson:1' }));
		expect(after).toEqual({ ...photo.settings, Device: 'epson:1' });
		expect(after.Brightness).toBeUndefined();
	});
});

describe('loading stored preferences', () => {
	const fake = (entries: Record<string, string>): Storage =>
		({
			getItem: (k: string) => entries[k] ?? null,
			setItem: (k: string, v: string) => void (entries[k] = v)
		}) as unknown as Storage;

	it('fills in a preference stored before it existed', () => {
		// A browser that saved its prefs before the metadata fields were added.
		const store = createExportPrefs(
			fake({ [EXPORT_KEY]: JSON.stringify({ format: 1, nameTemplate: '{YEAR}' }) })
		);
		const prefs = get(store);
		expect(prefs.nameTemplate).toBe('{YEAR}');
		expect(prefs.author).toBe(defaultExportPrefs.author);
	});

	it('keeps a stored value that happens to match a falsy default', () => {
		const store = createExportPrefs(fake({ [EXPORT_KEY]: JSON.stringify({ format: 0 }) }));
		expect(get(store).format).toBe(0);
	});

	it('replaces the preset list wholesale rather than merging the defaults back in', () => {
		const mine = [{ id: 'x', name: 'Mine', icon: 'star', settings: {} }];
		const store = createPresets(fake({ [PRESETS_KEY]: JSON.stringify(mine) }));
		expect(get(store)).toEqual(mine);
	});

	it('falls back when nothing is stored or the value is unusable', () => {
		expect(get(createExportPrefs(fake({})))).toEqual(defaultExportPrefs);
		expect(get(createExportPrefs(fake({ [EXPORT_KEY]: 'not json' })))).toEqual(defaultExportPrefs);
		expect(get(createPresets(fake({ [PRESETS_KEY]: '7' })))).toEqual(defaultPresets);
	});
});

describe('documentMetadata', () => {
	it('sends nothing when nothing is set, and only what is set otherwise', () => {
		expect(documentMetadata(defaultExportPrefs)).toBeUndefined();
		expect(documentMetadata({ ...defaultExportPrefs, author: '  ' })).toBeUndefined();
		expect(documentMetadata({ ...defaultExportPrefs, author: ' Jane ', keywords: 'tax' })).toEqual({
			Author: 'Jane',
			Subject: undefined,
			Keywords: 'tax'
		});
	});

	it('is absent from a browser that stored its preferences before it existed', () => {
		const stored: Record<string, string> = {
			[EXPORT_KEY]: JSON.stringify({ format: 0, directory: '/x' })
		};
		const storage = { getItem: (k: string) => stored[k] ?? null, setItem: () => undefined };
		const prefs = get(createExportPrefs(storage as unknown as Storage));
		expect(prefs.author).toBe('');
		expect(documentMetadata(prefs)).toBeUndefined();
	});
});
