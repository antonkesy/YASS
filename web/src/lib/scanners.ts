import type { ScannerDeviceDto, SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import type { KnownScanner, ScannerPrefs } from '$lib/presets';

/**
 * The scanners worth listing in Settings: every one the server lists now, every one this
 * browser has seen before, and every one a past scan ran on. A scanner that is not on the
 * server's list now is still shown — as a ghost — so it can be renamed, hidden or forgotten
 * while it is unplugged, and so its name is not lost the day it comes back.
 */
export type ScannerRow = {
	id: string;
	/** The name to show: this browser's label, else what the driver calls it, else the id. */
	name: string;
	/** What the driver calls it, when that differs from the label. */
	original: string;
	connected: boolean;
	hidden: boolean;
	labelled: boolean;
	driver?: string;
	connection?: string;
	firstSeen?: string;
	lastSeen?: string;
	/** When a past scan last ran on it, and how many did. */
	lastUsed?: string;
	scans: number;
};

/** Notes every scanner on the server's list as seen now, keeping what was known about the rest. */
export function remember(
	prefs: ScannerPrefs,
	devices: readonly ScannerDeviceDto[],
	now = new Date()
): ScannerPrefs {
	if (devices.length === 0) return prefs;
	const at = now.toISOString();
	const known = { ...prefs.known };
	for (const d of devices) {
		const before: KnownScanner | undefined = known[d.Id];
		known[d.Id] = {
			name: d.Name,
			driver: d.Driver || before?.driver,
			connection: d.Connection || before?.connection,
			firstSeen: before?.firstSeen ?? at,
			lastSeen: at
		};
	}
	return { ...prefs, known };
}

/** The name a scanner goes by in this browser. */
export function scannerName(prefs: ScannerPrefs, id: string, fallback?: string): string {
	return prefs.labels[id]?.trim() || fallback || prefs.known[id]?.name || id;
}

/**
 * The server's list as the home screen's picker shows it: renamed, and without the hidden
 * ones — except the one chosen now, which hiding must not make disappear from under a scan.
 */
export function pickable(
	prefs: ScannerPrefs,
	devices: readonly ScannerDeviceDto[],
	chosen?: string
): ScannerDeviceDto[] {
	return devices
		.filter((d) => d.Id === chosen || !prefs.hidden.includes(d.Id))
		.map((d) => ({ ...d, Name: scannerName(prefs, d.Id, d.Name) }));
}

export function scannerRows(
	prefs: ScannerPrefs,
	devices: readonly ScannerDeviceDto[],
	sessions: readonly SessionDto[]
): ScannerRow[] {
	const usage = new Map<string, { scans: number; last?: string }>();
	for (const s of sessions) {
		const id = s.OptionsUsed.Device;
		if (!id) continue;
		const u = usage.get(id) ?? { scans: 0 };
		u.scans += 1;
		if (!u.last || s.CreatedAt > u.last) u.last = s.CreatedAt;
		usage.set(id, u);
	}

	const ids = new Set([...devices.map((d) => d.Id), ...Object.keys(prefs.known), ...usage.keys()]);
	const rows = [...ids].map((id): ScannerRow => {
		const live = devices.find((d) => d.Id === id);
		const known = prefs.known[id];
		const original = live?.Name ?? known?.name ?? id;
		const u = usage.get(id);
		return {
			id,
			name: scannerName(prefs, id, original),
			original,
			connected: !!live,
			hidden: prefs.hidden.includes(id),
			labelled: !!prefs.labels[id]?.trim(),
			driver: live?.Driver ?? known?.driver,
			connection: live?.Connection ?? known?.connection,
			firstSeen: known?.firstSeen,
			lastSeen: live ? new Date().toISOString() : known?.lastSeen,
			lastUsed: u?.last,
			scans: u?.scans ?? 0
		};
	});

	// Connected ones first, in the server's order; then the ghosts, most recently seen first.
	const order = (r: ScannerRow) => devices.findIndex((d) => d.Id === r.id);
	return rows.sort((a, b) =>
		a.connected !== b.connected
			? a.connected
				? -1
				: 1
			: a.connected
				? order(a) - order(b)
				: (b.lastSeen ?? b.lastUsed ?? '').localeCompare(a.lastSeen ?? a.lastUsed ?? '')
	);
}

export function rename(prefs: ScannerPrefs, id: string, label: string): ScannerPrefs {
	const labels = { ...prefs.labels };
	const known = prefs.known[id]?.name;
	// A name set back to the driver's own, or emptied, is no label at all.
	if (!label.trim() || label.trim() === known) delete labels[id];
	else labels[id] = label.trim();
	return { ...prefs, labels };
}

export function setHidden(prefs: ScannerPrefs, id: string, hidden: boolean): ScannerPrefs {
	const rest = prefs.hidden.filter((h) => h !== id);
	return { ...prefs, hidden: hidden ? [...rest, id] : rest };
}

/** Drops everything this browser knew about a scanner that is gone. */
export function forget(prefs: ScannerPrefs, id: string): ScannerPrefs {
	const known = { ...prefs.known };
	const labels = { ...prefs.labels };
	delete known[id];
	delete labels[id];
	return { known, labels, hidden: prefs.hidden.filter((h) => h !== id) };
}
