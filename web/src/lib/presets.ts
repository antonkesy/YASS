import { writable } from 'svelte/store';
import { ColorMode, PageSize, ScanSource } from './hub/generated/Yass.Core.Domain';
import { ExportFormat } from './hub/generated/Yass.Core.Hub';
import type { PresetIconName } from './icons';
import type { ScanOverrides } from './settings';

/**
 * Named bundles of scan settings, kept per browser next to the overrides rather than
 * in yass.yaml: they are a convenience for whoever is standing at the scanner, and the
 * config file stays the operator's.
 */
export type Preset = {
	id: string;
	name: string;
	icon: PresetIconName;
	settings: ScanOverrides;
};

export const PRESETS_KEY = 'yass.presets';

/** What a fresh browser starts with. The names match the mockup's. */
export const defaultPresets: Preset[] = [
	{
		id: 'document',
		name: 'Document',
		icon: 'file',
		settings: { Source: ScanSource.Feeder, Color: ColorMode.Color, Dpi: 300, PageSize: PageSize.A4 }
	},
	{
		id: 'double-sided',
		name: 'Double-sided',
		icon: 'copy',
		settings: { Source: ScanSource.Duplex, Color: ColorMode.Grayscale, Dpi: 300, Contrast: 10 }
	},
	{
		id: 'receipt',
		name: 'Receipt',
		icon: 'receipt',
		settings: {
			Source: ScanSource.Flatbed,
			Color: ColorMode.Grayscale,
			Dpi: 300,
			Brightness: 10,
			Contrast: 20
		}
	},
	{
		id: 'photo',
		name: 'Photo',
		icon: 'image',
		settings: { Source: ScanSource.Flatbed, Color: ColorMode.Color, Dpi: 600 }
	}
];

/** Export preferences that are not scan options: where files go and what they are called. */
export type ExportPrefs = {
	format: ExportFormat;
	directory: string;
	nameTemplate: string;
	exportButton: boolean;
};

export const EXPORT_KEY = 'yass.export';

export const defaultExportPrefs: ExportPrefs = {
	format: ExportFormat.Pdf,
	directory: '',
	nameTemplate: '',
	exportButton: true
};

/** True when `settings` matches every key the preset pins. */
export function matchesPreset(preset: Preset, overrides: ScanOverrides): boolean {
	return Object.entries(preset.settings).every(
		([key, value]) => overrides[key as keyof ScanOverrides] === value
	);
}

export function activePreset(presets: readonly Preset[], overrides: ScanOverrides): Preset | null {
	return presets.find((p) => matchesPreset(p, overrides)) ?? null;
}

function load<T>(storage: Storage | undefined, key: string, fallback: T): T {
	try {
		const raw = storage?.getItem(key);
		if (!raw) return fallback;
		const parsed: unknown = JSON.parse(raw);
		return parsed && typeof parsed === 'object' ? (parsed as T) : fallback;
	} catch {
		return fallback;
	}
}

function persisted<T>(storage: Storage | undefined, key: string, fallback: T) {
	const store = writable<T>(load(storage, key, fallback));
	return {
		subscribe: store.subscribe,
		set(value: T) {
			store.set(value);
			try {
				storage?.setItem(key, JSON.stringify(value));
			} catch {
				// private mode or quota: keep the in-memory value
			}
		},
		reset() {
			this.set(fallback);
		}
	};
}

export const createPresets = (storage: Storage | undefined) =>
	persisted<Preset[]>(storage, PRESETS_KEY, defaultPresets);

export const createExportPrefs = (storage: Storage | undefined) =>
	persisted<ExportPrefs>(storage, EXPORT_KEY, defaultExportPrefs);

const storage = typeof localStorage === 'undefined' ? undefined : localStorage;
export const presets = createPresets(storage);
export const exportPrefs = createExportPrefs(storage);
