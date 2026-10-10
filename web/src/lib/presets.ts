import { t, type MessageKey } from './i18n/index.svelte';
import { ColorMode, PageSize, ScanSource } from './hub/generated/Yass.Core.Domain';
import { ExportFormat, type DocumentMetadataDto } from './hub/generated/Yass.Core.Hub';
import type { PresetIconName } from './icons';
import type { ScanOverrides } from './settings';
import { browserStorage, persisted } from './storage';

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

/**
 * A preset's name in the UI's language. The built-in presets are stored by their English
 * names; one still carrying that name is shown translated, while one the user renamed, and
 * every preset the user made, shows as they named it.
 */
export function presetName(preset: Pick<Preset, 'id' | 'name'>): string {
	const builtIn = defaultPresets.find((p) => p.id === preset.id);
	if (!builtIn || builtIn.name !== preset.name) return preset.name;
	return t(BUILT_IN_NAMES[preset.id]);
}

const BUILT_IN_NAMES: Record<string, MessageKey> = {
	document: 'common.presetDocument',
	'double-sided': 'common.presetDoubleSided',
	receipt: 'common.presetReceipt',
	photo: 'common.presetPhoto'
};

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

/** The PDF properties one scan can set for itself, over the ones Settings gives every scan. */
export type DocumentMeta = Partial<Pick<ExportPrefs, 'author' | 'subject' | 'keywords'>>;
export const DOCUMENT_META_FIELDS = ['author', 'subject', 'keywords'] as const;

export const DOCUMENT_META_KEY = 'yass.documentMeta';

/** The export preferences one scan saves with: Settings', with its own PDF properties on top. */
export function metaFor(
	prefs: ExportPrefs,
	all: Readonly<Record<string, DocumentMeta>>,
	sessionId: string
): ExportPrefs {
	return { ...prefs, ...all[sessionId] };
}

/**
 * One scan's PDF property set to `value`. A value equal to what Settings gives every scan is
 * no override at all, and a scan left with none is dropped, so the store only holds real ones.
 */
export function setDocumentMeta(
	all: Readonly<Record<string, DocumentMeta>>,
	sessionId: string,
	prefs: ExportPrefs,
	field: keyof DocumentMeta,
	value: string
): Record<string, DocumentMeta> {
	const own: DocumentMeta = { ...all[sessionId] };
	if (value === prefs[field]) delete own[field];
	else own[field] = value;
	const next = { ...all };
	if (Object.keys(own).length === 0) delete next[sessionId];
	else next[sessionId] = own;
	return next;
}

/** Drops the properties of scans that no longer exist, so the store does not grow forever. */
export function pruneDocumentMeta(
	all: Readonly<Record<string, DocumentMeta>>,
	existing: ReadonlySet<string>
): Record<string, DocumentMeta> {
	return Object.fromEntries(Object.entries(all).filter(([id]) => existing.has(id)));
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

export const createDocumentMeta = (storage: Storage | undefined) =>
	persisted<Record<string, DocumentMeta>>(storage, DOCUMENT_META_KEY, {});

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

const storage = browserStorage();
export const presets = createPresets(storage);
export const pickedPreset = createPickedPreset(storage);
export const exportPrefs = createExportPrefs(storage);
export const reviewPrefs = createReviewPrefs(storage);
export const historyPrefs = createHistoryPrefs(storage);
export const documentMeta = createDocumentMeta(storage);
export const scannerPrefs = createScannerPrefs(storage);
