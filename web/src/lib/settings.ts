import { writable } from 'svelte/store';
import type { ScanOptionsDto } from './hub/generated/Yass.Core.Hub';
import { browserStorage } from './storage';

/**
 * Per-browser scan overrides (SPEC §8: settings live in localStorage, never on the server):
 * the scan options proper, without the facts about the server that ride along on
 * `GetEffectiveOptions` (allowed downloads, export roots).
 */
export type ScanOverrides = Pick<ScanOptionsDto, (typeof editableKeys)[keyof typeof editableKeys]>;

export const STORAGE_KEY = 'yass.scanOverrides';

/** snake_case config key → DTO property: the scan options a browser may override. */
export const editableKeys = {
	source: 'Source',
	dpi: 'Dpi',
	color: 'Color',
	page_size: 'PageSize',
	brightness: 'Brightness',
	contrast: 'Contrast',
	blank_page_detection: 'BlankPageDetection',
	blank_page_threshold: 'BlankPageThreshold',
	auto_deskew: 'AutoDeskew',
	device: 'Device'
} as const satisfies Record<string, keyof ScanOptionsDto>;

/**
 * Just the scan options out of a full options DTO. What a preset is matched against and
 * saved from: a preset that pinned the server's export roots would never match again.
 */
export function scanOverrides(options: ScanOptionsDto): ScanOverrides {
	const known = new Set<string>(Object.values(editableKeys));
	return Object.fromEntries(
		Object.entries(options).filter(([k, v]) => known.has(k) && v != null)
	) as ScanOverrides;
}

export function parseOverrides(json: string | null): ScanOverrides {
	if (!json) return {};
	try {
		const parsed: unknown = JSON.parse(json);
		if (!parsed || typeof parsed !== 'object') return {};
		const known = new Set<string>(Object.values(editableKeys));
		return Object.fromEntries(
			Object.entries(parsed).filter(([k, v]) => known.has(k) && v != null)
		) as ScanOverrides;
	} catch {
		return {};
	}
}

function read(storage: Storage | undefined): ScanOverrides {
	try {
		return parseOverrides(storage?.getItem(STORAGE_KEY) ?? null);
	} catch {
		return {};
	}
}

export function createSettings(storage: Storage | undefined) {
	const store = writable<ScanOverrides>(read(storage));
	return {
		subscribe: store.subscribe,
		set(overrides: ScanOverrides) {
			const clean = Object.fromEntries(
				Object.entries(overrides).filter(([, v]) => v !== undefined && v !== null)
			) as ScanOverrides;
			store.set(clean);
			try {
				if (Object.keys(clean).length === 0) storage?.removeItem(STORAGE_KEY);
				else storage?.setItem(STORAGE_KEY, JSON.stringify(clean));
			} catch {
				// private mode or quota: keep the in-memory value
			}
		},
		update<K extends keyof ScanOverrides>(key: K, value: ScanOverrides[K] | undefined) {
			let next: ScanOverrides = {};
			store.update((current) => (next = { ...current, [key]: value }));
			this.set(next);
		},
		reset() {
			this.set({});
		}
	};
}

export const settings = createSettings(browserStorage());
