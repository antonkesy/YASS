import { describe, expect, it } from 'vitest';
import { get } from 'svelte/store';
import {
	createSettings,
	parseOverrides,
	pickOverrides,
	scanOverrides,
	STORAGE_KEY
} from '$lib/settings';
import { persisted } from '$lib/storage';

function fakeStorage(initial: Record<string, string> = {}): Storage {
	const data = new Map(Object.entries(initial));
	return {
		get length() {
			return data.size;
		},
		clear: () => data.clear(),
		getItem: (k) => data.get(k) ?? null,
		key: (i) => [...data.keys()][i] ?? null,
		removeItem: (k) => void data.delete(k),
		setItem: (k, v) => void data.set(k, v)
	};
}

describe('settings', () => {
	it('parses only known keys and survives garbage', () => {
		expect(parseOverrides(null)).toEqual({});
		expect(parseOverrides('{not json')).toEqual({});
		expect(parseOverrides('{"Dpi":600,"Bogus":1,"Color":null}')).toEqual({ Dpi: 600 });
	});

	it('keeps only the scan options out of the effective options', () => {
		expect(
			scanOverrides({ Dpi: 300, Device: 'epson:1', Color: undefined, BlankPageDetection: null! })
		).toEqual({ Dpi: 300, Device: 'epson:1' });
	});

	it('round-trips through storage and clears on reset', () => {
		const storage = fakeStorage();
		const s = createSettings(storage);
		s.update('Dpi', 600);
		s.update('AutoDeskew', true);
		expect(JSON.parse(storage.getItem(STORAGE_KEY)!)).toEqual({ Dpi: 600, AutoDeskew: true });
		expect(get(createSettings(storage))).toEqual({ Dpi: 600, AutoDeskew: true });
		s.update('Dpi', undefined);
		expect(get(s)).toEqual({ AutoDeskew: true });
		s.reset();
		expect(storage.getItem(STORAGE_KEY)).toBeNull();
	});

	it('tells subscribers about an update once, already cleaned', () => {
		const s = createSettings(fakeStorage());
		s.update('Dpi', 600);
		const heard: unknown[] = [];
		const stop = s.subscribe((v) => heard.push(v));
		s.update('Dpi', undefined);
		stop();
		expect(heard).toEqual([{ Dpi: 600 }, {}]);
	});

	it('drops unknown and empty keys a stored value carries', () => {
		const storage = fakeStorage({ [STORAGE_KEY]: '{"Dpi":600,"Bogus":1,"Color":null}' });
		expect(get(createSettings(storage))).toEqual({ Dpi: 600 });
		expect(pickOverrides({ Contrast: 0, Old: true })).toEqual({ Contrast: 0 });
	});

	it('works without storage', () => {
		const s = createSettings(undefined);
		s.update('Dpi', 300);
		expect(get(s)).toEqual({ Dpi: 300 });
	});
});

describe('persisted', () => {
	it('stores every set, and an empty object only when asked to keep it', () => {
		const storage = fakeStorage();
		const kept = persisted<Record<string, number>>(storage, 'kept', {});
		kept.set({});
		expect(storage.getItem('kept')).toBe('{}');
		const removed = persisted<Record<string, number>>(storage, 'gone', {}, { removeEmpty: true });
		removed.set({ a: 1 });
		expect(storage.getItem('gone')).toBe('{"a":1}');
		removed.reset();
		expect(storage.getItem('gone')).toBeNull();
	});

	it('cleans what it loads and what it is given', () => {
		const storage = fakeStorage({ k: '{"a":1,"b":-1}' });
		const positive = (v: Record<string, number>) =>
			Object.fromEntries(Object.entries(v).filter(([, n]) => n > 0));
		const s = persisted<Record<string, number>>(storage, 'k', {}, { clean: positive });
		expect(get(s)).toEqual({ a: 1 });
		s.set({ a: -2, c: 3 });
		expect(get(s)).toEqual({ c: 3 });
		expect(storage.getItem('k')).toBe('{"c":3}');
	});
});
