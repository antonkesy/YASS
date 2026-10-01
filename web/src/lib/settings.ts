import { writable } from 'svelte/store';
import type { ScanOptionsDto } from './hub/generated/Yass.Core.Hub';

/** Per-browser scan overrides (SPEC §8: settings live in localStorage, never on the server). */
export type ScanOverrides = ScanOptionsDto;

export const STORAGE_KEY = 'yass.scanOverrides';

/** snake_case config key → DTO property: the scan options a browser may override. */
export const editableKeys: Record<string, keyof ScanOverrides> = {
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
};

export function parseOverrides(json: string | null): ScanOverrides {
	if (!json) return {};
	try {
		const parsed: unknown = JSON.parse(json);
		if (!parsed || typeof parsed !== 'object') return {};
		const known = new Set(Object.values(editableKeys));
		return Object.fromEntries(
			Object.entries(parsed).filter(([k, v]) => known.has(k as keyof ScanOverrides) && v != null)
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

export const settings = createSettings(
	typeof localStorage === 'undefined' ? undefined : localStorage
);
