import { t, type MessageKey } from './i18n/index.svelte';
import { ColorMode, PageSize, ScanSource } from './hub/generated/Yass.Core.Domain';
import { ExportFormat, type DocumentMetadataDto } from './hub/generated/Yass.Core.Hub';
import type { PresetIconName } from './icons';
import type { ScanOverrides } from './settings';
import { browserStorage, persisted } from './storage';

export type Preset = {
	id: string;
	name: string;
	icon: PresetIconName;
	settings: ScanOverrides;
	/** Absent means offered for every scanner. The scanner itself is never in `settings`. */
	device?: string;
};

export const PRESETS_KEY = 'yass.presets';

/** Built-ins still carrying their stored English name are shown translated. */
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

export type ExportPrefs = {
	format: ExportFormat;
	nameTemplate: string;
	author: string;
	subject: string;
	keywords: string;
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

/** Undefined when nothing is set, so the server's defaults apply. */
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

export type DocumentMeta = Partial<Pick<ExportPrefs, 'author' | 'subject' | 'keywords'>>;
export const DOCUMENT_META_FIELDS = ['author', 'subject', 'keywords'] as const;

export const DOCUMENT_META_LABELS: Record<
	keyof DocumentMeta,
	{ label: MessageKey; placeholder: MessageKey }
> = {
	author: { label: 'settings.author', placeholder: 'settings.authorPlaceholder' },
	subject: { label: 'settings.subject', placeholder: 'settings.subjectPlaceholder' },
	keywords: { label: 'settings.keywords', placeholder: 'settings.keywordsPlaceholder' }
};

export const DOCUMENT_META_KEY = 'yass.documentMeta';

export function metaFor(
	prefs: ExportPrefs,
	all: Readonly<Record<string, DocumentMeta>>,
	sessionId: string
): ExportPrefs {
	return { ...prefs, ...all[sessionId] };
}

/** A value equal to the Settings default is dropped, so the store only holds real overrides. */
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

export function pruneDocumentMeta(
	all: Readonly<Record<string, DocumentMeta>>,
	existing: ReadonlySet<string>
): Record<string, DocumentMeta> {
	return Object.fromEntries(Object.entries(all).filter(([id]) => existing.has(id)));
}

export type ReviewPrefs = {
	/** `'fit'` until someone zooms: a remembered size cannot know the next scan's page count. */
	overviewTile: number | 'fit';
};

export const REVIEW_KEY = 'yass.review';

export const defaultReviewPrefs: ReviewPrefs = { overviewTile: 'fit' };

export function matchesPreset(preset: Preset, overrides: ScanOverrides): boolean {
	const pinned = Object.entries(preset.settings);
	// every() is true on an empty list; a preset pinning nothing must match nothing.
	if (pinned.length === 0) return false;
	return pinned.every(([key, value]) => overrides[key as keyof ScanOverrides] === value);
}

/** Several can match: the last picked wins while it matches, else the one pinning most keys. */
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

/** Replaces rather than merges, so keys of the previous preset do not leak into this one. */
export function applyPreset(preset: Preset, current: ScanOverrides): ScanOverrides {
	return current.Device ? { ...preset.settings, Device: current.Device } : { ...preset.settings };
}

export function presetsFor(presets: readonly Preset[], device: string | undefined): Preset[] {
	return presets.filter((p) => !p.device || p.device === device);
}

export type HistoryPrefs = {
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

export type PickedPreset = { id: string | null };

export const PICKED_KEY = 'yass.pickedPreset';

export const createPickedPreset = (storage: Storage | undefined) =>
	persisted<PickedPreset>(storage, PICKED_KEY, { id: null });

export const createDocumentMeta = (storage: Storage | undefined) =>
	persisted<Record<string, DocumentMeta>>(storage, DOCUMENT_META_KEY, {});

export const createHistoryPrefs = (storage: Storage | undefined) =>
	persisted<HistoryPrefs>(storage, HISTORY_KEY, defaultHistoryPrefs);

export type KnownScanner = {
	name: string;
	driver?: string;
	connection?: string;
	firstSeen: string;
	lastSeen: string;
};

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
