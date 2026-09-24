import { describe, expect, it } from 'vitest';
import { get } from 'svelte/store';
import { createSettings, parseOverrides, pickEditable, STORAGE_KEY } from '$lib/settings';

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

	it('picks only keys the server marks editable', () => {
		expect(pickEditable({ Dpi: 600, Source: 2, Brightness: 5 }, ['dpi', 'source'])).toEqual({
			Dpi: 600,
			Source: 2
		});
		expect(pickEditable({ Dpi: 600 }, undefined)).toEqual({});
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

	it('works without storage', () => {
		const s = createSettings(undefined);
		s.update('Dpi', 300);
		expect(get(s)).toEqual({ Dpi: 300 });
	});
});
