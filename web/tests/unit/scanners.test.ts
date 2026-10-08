import { describe, expect, it } from 'vitest';
import { defaultScannerPrefs, type ScannerPrefs } from '$lib/presets';
import {
	forget,
	pickable,
	remember,
	rename,
	scannerName,
	scannerRows,
	setHidden
} from '$lib/scanners';
import { session } from './fixtures';

const epson = { Id: 'epson:1', Name: 'Epson DS-530', Driver: 'sane' };
const canon = {
	Id: 'canon:2',
	Name: 'Canon LiDE 400',
	Driver: 'escl',
	Connection: 'http://10.0.0.9'
};
const t0 = new Date('2026-10-01T10:00:00Z');
const t1 = new Date('2026-10-05T10:00:00Z');

describe('remembering scanners', () => {
	it('keeps when each was first and last seen, and what the driver said about it', () => {
		let prefs = remember(defaultScannerPrefs, [epson, canon], t0);
		prefs = remember(prefs, [{ Id: 'epson:1', Name: 'Epson DS-530' }], t1);

		expect(prefs.known['epson:1']).toEqual({
			name: 'Epson DS-530',
			driver: 'sane',
			firstSeen: t0.toISOString(),
			lastSeen: t1.toISOString()
		});
		// Not on the list the second time: still known, from when it last was.
		expect(prefs.known['canon:2'].lastSeen).toBe(t0.toISOString());
	});

	it('changes nothing when the server lists no scanner at all', () => {
		const prefs = remember(defaultScannerPrefs, [epson], t0);
		expect(remember(prefs, [], t1)).toBe(prefs);
	});
});

describe('naming and hiding', () => {
	const seen = remember(defaultScannerPrefs, [epson, canon], t0);

	it('goes by its label, and a label set back to the driver name is no label', () => {
		const named = rename(seen, 'epson:1', '  Office  ');
		expect(scannerName(named, 'epson:1')).toBe('Office');
		expect(rename(named, 'epson:1', 'Epson DS-530').labels).toEqual({});
		expect(rename(named, 'epson:1', '').labels).toEqual({});
		expect(scannerName(seen, 'unknown:9')).toBe('unknown:9');
	});

	it('leaves hidden ones out of the picker, but never the one chosen now', () => {
		const hidden = setHidden(rename(seen, 'canon:2', 'Photos'), 'epson:1', true);

		expect(pickable(hidden, [epson, canon]).map((d) => d.Name)).toEqual(['Photos']);
		expect(pickable(hidden, [epson, canon], 'epson:1').map((d) => d.Id)).toEqual([
			'epson:1',
			'canon:2'
		]);
		expect(setHidden(hidden, 'epson:1', false).hidden).toEqual([]);
	});

	it('forgets a scanner along with its name and hiding', () => {
		const prefs: ScannerPrefs = setHidden(rename(seen, 'canon:2', 'Photos'), 'canon:2', true);
		expect(forget(prefs, 'canon:2')).toEqual({
			known: { 'epson:1': seen.known['epson:1'] },
			labels: {},
			hidden: []
		});
	});
});

describe('the list in Settings', () => {
	it('lists connected scanners first, then ghosts, with how often each was used', () => {
		const prefs = remember(defaultScannerPrefs, [epson, canon], t0);
		const scans = [
			session('a', { CreatedAt: '2026-10-02T09:00:00Z', OptionsUsed: { Device: 'canon:2' } }),
			session('b', { CreatedAt: '2026-10-03T09:00:00Z', OptionsUsed: { Device: 'canon:2' } }),
			// Used once, long ago, and never seen by this browser: listed all the same.
			session('c', { CreatedAt: '2026-09-01T09:00:00Z', OptionsUsed: { Device: 'old:7' } })
		];

		const rows = scannerRows(prefs, [epson], scans);

		expect(rows.map((r) => [r.id, r.connected])).toEqual([
			['epson:1', true],
			['canon:2', false],
			['old:7', false]
		]);
		expect(rows[1]).toMatchObject({ scans: 2, lastUsed: '2026-10-03T09:00:00Z', driver: 'escl' });
		expect(rows[1].connection).toBe('http://10.0.0.9');
		expect(rows[2]).toMatchObject({ name: 'old:7', scans: 1 });
	});
});
