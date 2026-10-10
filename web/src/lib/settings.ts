import { get } from 'svelte/store';
import type { ScanOptionsDto } from './hub/generated/Yass.Core.Hub';
import { browserStorage, persisted } from './storage';

/** SPEC §8: settings live in localStorage only. Absent keys fall back to yass.yaml. */
export type ScanOverrides = Pick<ScanOptionsDto, (typeof overrideKeys)[number]>;

export const STORAGE_KEY = 'yass.scanOverrides';

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

export function pickOverrides(source: object): ScanOverrides {
	return Object.fromEntries(
		Object.entries(source).filter(([k, v]) => known.has(k) && v != null)
	) as ScanOverrides;
}

/** Keeps fields the server adds to the DTO later out of presets. */
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
		update<K extends keyof ScanOverrides>(key: K, value: ScanOverrides[K] | undefined) {
			store.set({ ...get(store), [key]: value });
		}
	};
}

export const settings = createSettings(browserStorage());
