import { describe, expect, it } from 'vitest';
import { get } from 'svelte/store';
import { createSettings, parseOverrides, scanOverrides, STORAGE_KEY } from '$lib/settings';

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

	it('works without storage', () => {
		const s = createSettings(undefined);
		s.update('Dpi', 300);
		expect(get(s)).toEqual({ Dpi: 300 });
	});
});
