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

/** Export preferences that are not scan options: what a downloaded file is and what it is called. */
export type ExportPrefs = {
	format: ExportFormat;
	nameTemplate: string;
	/** Stamped into every PDF saved from this browser. The title is the document's name. */
	author: string;
	subject: string;
	keywords: string;
	/**
	 * Whether this browser offers Save to server next to Save as…. Only shown in Settings, and
	 * only acted on, when the server has an output.directory configured.
	 */
	serverSave: boolean;
};

export const EXPORT_KEY = 'yass.export';

export const defaultExportPrefs: ExportPrefs = {
	format: ExportFormat.Pdf,
	nameTemplate: '',
	author: '',
	subject: '',
	keywords: '',
	serverSave: false
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

/**
 * The preset the next scan is running, if any. Presets overlap — one saved while Document was
 * lit pins Document's keys and more — so several can match at once. The one last picked wins
 * while it still matches; otherwise the one that pins the most, so a saved preset is not
 * forever hidden behind the built-in it was made from.
 */
export function activePreset(
	presets: readonly Preset[],
	overrides: ScanOverrides,
	picked: string | null = null
): Preset | null {
	const matching = presets.filter((p) => matchesPreset(p, overrides));
	const preferred = matching.find((p) => p.id === picked);
	if (preferred) return preferred;
	const pins = (p: Preset) => Object.keys(p.settings).length;
	return matching.reduce<Preset | null>(
		(best, p) => (!best || pins(p) > pins(best) ? p : best),
		null
	);
}

/**
 * The overrides after picking a preset: exactly what it pins, and the scanner, which a preset
 * never carries. Merged over the old overrides instead, a Receipt's brightness rode along into
 * the Photo picked after it, and the pill said Photo while the scan said otherwise.
 */
export function applyPreset(preset: Preset, current: ScanOverrides): ScanOverrides {
	return current.Device ? { ...preset.settings, Device: current.Device } : { ...preset.settings };
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

/**
 * The preset this browser last picked, so it stays lit when another one matches as well, and
 * can be shown as edited rather than simply vanishing once a setting is changed.
 */
export type PickedPreset = { id: string | null };

export const PICKED_KEY = 'yass.pickedPreset';

export const createPickedPreset = (storage: Storage | undefined) =>
	persisted<PickedPreset>(storage, PICKED_KEY, { id: null });

export const createHistoryPrefs = (storage: Storage | undefined) =>
	persisted<HistoryPrefs>(storage, HISTORY_KEY, defaultHistoryPrefs);

/** A scanner this browser has seen, remembered so it can be listed after it goes away. */
export type KnownScanner = {
	/** What the driver calls it, as last seen. */
	name: string;
	driver?: string;
	connection?: string;
	/** ISO times this browser first and last saw it on the server's list. */
	firstSeen: string;
	lastSeen: string;
};

/**
 * What this browser knows and has decided about scanners: the ones it has seen, the names it
 * gave them, and the ones it hid from the picker. Kept here rather than on the server like the
 * rest of this browser's preferences: a name is whoever is standing at it's business.
 */
export type ScannerPrefs = {
	known: Record<string, KnownScanner>;
	labels: Record<string, string>;
	hidden: string[];
};

export const SCANNERS_KEY = 'yass.scanners';

export const defaultScannerPrefs: ScannerPrefs = { known: {}, labels: {}, hidden: [] };

export const createScannerPrefs = (storage: Storage | undefined) =>
	persisted<ScannerPrefs>(storage, SCANNERS_KEY, defaultScannerPrefs);

const storage = typeof localStorage === 'undefined' ? undefined : localStorage;
export const presets = createPresets(storage);
export const pickedPreset = createPickedPreset(storage);
export const exportPrefs = createExportPrefs(storage);
export const reviewPrefs = createReviewPrefs(storage);
export const historyPrefs = createHistoryPrefs(storage);
export const scannerPrefs = createScannerPrefs(storage);
