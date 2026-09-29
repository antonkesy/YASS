import { writable } from 'svelte/store';
import { ColorMode, PageSize, ScanSource } from './hub/generated/Yass.Core.Domain';
import { ExportFormat } from './hub/generated/Yass.Core.Hub';
import type { PresetIconName } from './icons';
import type { NameStyle } from './naming/style';
import type { AiProvider } from './naming/types';
import { DEFAULT_TILE } from './review/overview';
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
	/**
	 * How a found name joins its words once it becomes a file name. Here rather than in
	 * `AiPrefs` because it shapes a file name, and applies to the heading read off the page
	 * as much as to anything a model wrote.
	 */
	nameStyle: NameStyle;
	exportButton: boolean;
};

export const EXPORT_KEY = 'yass.export';

export const defaultExportPrefs: ExportPrefs = {
	format: ExportFormat.Pdf,
	directory: '',
	nameTemplate: '',
	nameStyle: 'words',
	exportButton: true
};

/**
 * How this browser asks a model for a document name (ADR-008). Settings only — the key is
 * kept apart in `AiKeys`, so spreading this object can never leak it into a prop or a log.
 */
export type AiPrefs = {
	/**
	 * Whether Find name is offered at all. Off hides the button, and then nothing is ever read:
	 * the server only reads a page when the button asks it to, never during a scan.
	 */
	findName: boolean;
	provider: AiProvider;
	/** Base URL for the OpenAI-compatible shape. Empty falls back per provider. */
	baseUrl: string;
	/** Model per provider, so switching away and back does not lose the one you had. */
	models: Partial<Record<AiProvider, string>>;
};

/** API keys, alone in their own blob. Never logged, never passed whole into a component. */
export type AiKeys = {
	anthropic: string;
	openai: string;
};

export const AI_KEY = 'yass.ai';
export const AI_KEYS_KEY = 'yass.ai.keys';

export const defaultAiPrefs: AiPrefs = {
	findName: true,
	provider: 'off',
	baseUrl: '',
	models: {}
};

export const defaultAiKeys: AiKeys = { anthropic: '', openai: '' };

/** Review-screen preferences, next to the export ones so they share the same storage helper. */
export type ReviewPrefs = {
	/** Tile width the all-pages overview was last zoomed to. */
	overviewTile: number;
};

export const REVIEW_KEY = 'yass.review';

export const defaultReviewPrefs: ReviewPrefs = { overviewTile: DEFAULT_TILE };

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
		if (!parsed || typeof parsed !== 'object') return fallback;
		// Merged over the defaults, not used as-is: a browser that stored these before a
		// preference existed would otherwise read it as undefined, and the screens that
		// consume it disagree about what undefined means. Arrays (the presets) are
		// replaced wholesale — merging one key-by-key would splice the defaults back in.
		if (Array.isArray(fallback) || Array.isArray(parsed)) return parsed as T;
		return { ...fallback, ...parsed };
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

export const createReviewPrefs = (storage: Storage | undefined) =>
	persisted<ReviewPrefs>(storage, REVIEW_KEY, defaultReviewPrefs);

export const createAiPrefs = (storage: Storage | undefined) =>
	persisted<AiPrefs>(storage, AI_KEY, defaultAiPrefs);

export const createAiKeys = (storage: Storage | undefined) =>
	persisted<AiKeys>(storage, AI_KEYS_KEY, defaultAiKeys);

const storage = typeof localStorage === 'undefined' ? undefined : localStorage;
export const presets = createPresets(storage);
export const exportPrefs = createExportPrefs(storage);
export const reviewPrefs = createReviewPrefs(storage);
export const aiPrefs = createAiPrefs(storage);
export const aiKeys = createAiKeys(storage);
