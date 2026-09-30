import { describe, expect, it } from 'vitest';
import { ColorMode, PageSize, ScanSource } from '$lib/hub/generated/Yass.Core.Domain';
import {
	AI_KEY,
	AI_KEYS_KEY,
	EXPORT_KEY,
	PRESETS_KEY,
	activePreset,
	createAiKeys,
	createAiPrefs,
	createExportPrefs,
	createPresets,
	defaultAiKeys,
	defaultAiPrefs,
	defaultExportPrefs,
	defaultPresets,
	matchesPreset
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

describe('loading stored preferences', () => {
	const fake = (entries: Record<string, string>): Storage =>
		({
			getItem: (k: string) => entries[k] ?? null,
			setItem: (k: string, v: string) => void (entries[k] = v)
		}) as unknown as Storage;

	it('fills in a preference stored before it existed', () => {
		// A browser that saved its prefs before exportButton was added.
		const store = createExportPrefs(
			fake({ [EXPORT_KEY]: JSON.stringify({ format: 0, directory: '/data/consume' }) })
		);
		const prefs = get(store);
		expect(prefs.directory).toBe('/data/consume');
		expect(prefs.exportButton).toBe(defaultExportPrefs.exportButton);
	});

	it('keeps the model settings and the key in separate blobs', () => {
		const entries: Record<string, string> = {};
		const store = fake(entries);
		const prefs = createAiPrefs(store);
		const keys = createAiKeys(store);

		prefs.set({ ...defaultAiPrefs, provider: 'anthropic', models: { anthropic: 'claude-x' } });
		keys.set({ ...defaultAiKeys, anthropic: 'sk-secret' });

		// Two keys, and the one holding the secret is not the one the settings write to.
		expect(entries[AI_KEY]).not.toContain('sk-secret');
		expect(entries[AI_KEYS_KEY]).toContain('sk-secret');

		// Resetting the settings leaves the key where it was: they are cleared separately.
		prefs.reset();
		expect(get(prefs).provider).toBe('off');
		expect(get(keys).anthropic).toBe('sk-secret');
	});

	it('fills in a model preference stored before the provider existed', () => {
		// `load` merges over the defaults, so an older blob reads as its defaults, not undefined.
		const store = createAiPrefs(fake({ [AI_KEY]: JSON.stringify({ provider: 'openai' }) }));
		const prefs = get(store);
		expect(prefs.provider).toBe('openai');
		expect(prefs.models).toEqual({});
		expect(prefs.findName).toBe(defaultAiPrefs.findName);
	});

	it('keeps a stored value that happens to match a falsy default', () => {
		const store = createExportPrefs(
			fake({ [EXPORT_KEY]: JSON.stringify({ exportButton: false }) })
		);
		expect(get(store).exportButton).toBe(false);
	});

	it('fills in a name style stored before the setting existed', () => {
		// A browser that has been here since before this change must keep naming as it did.
		const store = createExportPrefs(
			fake({ [EXPORT_KEY]: JSON.stringify({ nameTemplate: '{YEAR}_scan' }) })
		);
		const prefs = get(store);
		expect(prefs.nameTemplate).toBe('{YEAR}_scan');
		expect(prefs.nameStyle).toBe('words');
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
