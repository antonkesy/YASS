import { writable } from 'svelte/store';
import { ColorMode, PageSize, ScanSource } from './hub/generated/Yass.Core.Domain';
import { ExportFormat, type DocumentMetadataDto } from './hub/generated/Yass.Core.Hub';
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
	/**
	 * The scanner this preset belongs to, and the only one it is offered for. Absent means
	 * every scanner — which is what the built-in presets are, and what every preset saved
	 * before a scanner could be chosen stays. The scanner itself is never in `settings`:
	 * it is picked on the home screen and the presets follow it, not the other way round.
	 */
	device?: string;
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
	/** Stamped into every PDF saved from this browser. The title is the document's name. */
	author: string;
	subject: string;
	keywords: string;
};

export const EXPORT_KEY = 'yass.export';

export const defaultExportPrefs: ExportPrefs = {
	format: ExportFormat.Pdf,
	directory: '',
	nameTemplate: '',
	exportButton: true,
	author: '',
	subject: '',
	keywords: ''
};

/**
 * The metadata an export request carries, from the preferences. Nothing at all when none is
 * set, so the server's own defaults apply rather than three empty strings.
 */
export function documentMetadata(prefs: ExportPrefs): DocumentMetadataDto | undefined {
	const author = prefs.author.trim();
	const subject = prefs.subject.trim();
	const keywords = prefs.keywords.trim();
	if (!author && !subject && !keywords) return undefined;
	return {
		Author: author || undefined,
		Subject: subject || undefined,
		Keywords: keywords || undefined
	};
}

/** Review-screen preferences, next to the export ones so they share the same storage helper. */
export type ReviewPrefs = {
	/**
	 * Tile width the all-pages overview was last zoomed to, or `'fit'` for whatever puts the
	 * whole scan on one screen. It stays `'fit'` until someone zooms: the first useful thing
	 * that view can do is show you everything at once, and a remembered size cannot know how
	 * many pages the next scan will have.
	 */
	overviewTile: number | 'fit';
};

export const REVIEW_KEY = 'yass.review';

export const defaultReviewPrefs: ReviewPrefs = { overviewTile: 'fit' };

/** True when `settings` matches every key the preset pins. */
export function matchesPreset(preset: Preset, overrides: ScanOverrides): boolean {
	const pinned = Object.entries(preset.settings);
	// A preset that pins nothing describes no scan in particular, so it matches none: every()
	// over an empty list says yes to anything, and the stage would light it up whatever the
	// settings were. Reachable since the scanner stopped counting as one of a preset's keys.
	if (pinned.length === 0) return false;
	return pinned.every(([key, value]) => overrides[key as keyof ScanOverrides] === value);
}

export function activePreset(presets: readonly Preset[], overrides: ScanOverrides): Preset | null {
	return presets.find((p) => matchesPreset(p, overrides)) ?? null;
}

/**
 * The presets worth offering for one scanner: the ones that belong to it, plus the ones
 * that belong to none. With no scanner chosen only the latter are left, which for a browser
 * that has never seen this feature is all of them.
 */
export function presetsFor(presets: readonly Preset[], device: string | undefined): Preset[] {
	return presets.filter((p) => !p.device || p.device === device);
}

/** Past-scans preferences: for now, only whether and when old scans go on their own. */
export type HistoryPrefs = {
	/**
	 * Scans older than this are deleted by whichever browser has it set, next time it looks at
	 * the list. `null` keeps them until someone deletes them or the server's own retention does.
	 */
	autoClear: { amount: number; unit: RetentionUnit } | null;
};

export type RetentionUnit = 'minutes' | 'hours' | 'days' | 'months';

export const HISTORY_KEY = 'yass.history';

export const defaultHistoryPrefs: HistoryPrefs = { autoClear: null };

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

export const createHistoryPrefs = (storage: Storage | undefined) =>
	persisted<HistoryPrefs>(storage, HISTORY_KEY, defaultHistoryPrefs);

const storage = typeof localStorage === 'undefined' ? undefined : localStorage;
export const presets = createPresets(storage);
export const exportPrefs = createExportPrefs(storage);
export const reviewPrefs = createReviewPrefs(storage);
export const historyPrefs = createHistoryPrefs(storage);
