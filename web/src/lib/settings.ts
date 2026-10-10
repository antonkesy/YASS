import { get } from 'svelte/store';
import type { ScanOptionsDto } from './hub/generated/Yass.Core.Hub';
import { browserStorage, persisted } from './storage';

/**
 * Per-browser scan overrides (SPEC §8: settings live in localStorage, never on the server):
 * each scan option this browser has set, and none it has not — an absent one falls back to
 * scanner.defaults in yass.yaml.
 */
export type ScanOverrides = Pick<ScanOptionsDto, (typeof overrideKeys)[number]>;

export const STORAGE_KEY = 'yass.scanOverrides';

/** The scan options a browser may override. */
export const overrideKeys = [
	'Source',
	'Dpi',
	'Color',
	'PageSize',
	'Brightness',
	'Contrast',
	'BlankPageDetection',
	'BlankPageThreshold',
	'AutoDeskew',
	'Device'
] as const satisfies readonly (keyof ScanOptionsDto)[];

const known = new Set<string>(overrideKeys);

/** The override keys out of anything, each with a value: what is stored, and what is matched. */
export function pickOverrides(source: object): ScanOverrides {
	return Object.fromEntries(
		Object.entries(source).filter(([k, v]) => known.has(k) && v != null)
	) as ScanOverrides;
}

/**
 * Just the scan options out of a full options DTO. What a preset is matched against and
 * saved from, so nothing the server adds to the DTO later rides along into a preset.
 */
export function scanOverrides(options: ScanOptionsDto): ScanOverrides {
	return pickOverrides(options);
}

export function parseOverrides(json: string | null): ScanOverrides {
	if (!json) return {};
	try {
		const parsed: unknown = JSON.parse(json);
		return parsed && typeof parsed === 'object' ? pickOverrides(parsed) : {};
	} catch {
		return {};
	}
}

export function createSettings(storage: Storage | undefined) {
	// Cleaned on the way in and out, and gone from storage altogether once nothing is set.
	const store = persisted<ScanOverrides>(
		storage,
		STORAGE_KEY,
		{},
		{
			clean: pickOverrides,
			removeEmpty: true
		}
	);
	return {
		...store,
		/** One option changed, the rest kept. Subscribers hear once, and only the cleaned value. */
		update<K extends keyof ScanOverrides>(key: K, value: ScanOverrides[K] | undefined) {
			store.set({ ...get(store), [key]: value });
		}
	};
}

export const settings = createSettings(browserStorage());
