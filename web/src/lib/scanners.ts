import type { ScannerDeviceDto, SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import type { KnownScanner, ScannerPrefs } from '$lib/presets';

/** Includes disconnected ("ghost") scanners so they can still be renamed, hidden or forgotten. */
export type ScannerRow = {
	id: string;
	name: string;
	original: string;
	connected: boolean;
	hidden: boolean;
	labelled: boolean;
	driver?: string;
	connection?: string;
	firstSeen?: string;
	lastSeen?: string;
	lastUsed?: string;
	scans: number;
};

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

export function scannerName(prefs: ScannerPrefs, id: string, fallback?: string): string {
	return prefs.labels[id]?.trim() || fallback || prefs.known[id]?.name || id;
}

/** Hidden scanners are left out, except the chosen one, which must not vanish from under a scan. */
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
	if (!label.trim() || label.trim() === known) delete labels[id];
	else labels[id] = label.trim();
	return { ...prefs, labels };
}

export function setHidden(prefs: ScannerPrefs, id: string, hidden: boolean): ScannerPrefs {
	const rest = prefs.hidden.filter((h) => h !== id);
	return { ...prefs, hidden: hidden ? [...rest, id] : rest };
}

export function forget(prefs: ScannerPrefs, id: string): ScannerPrefs {
	const known = { ...prefs.known };
	const labels = { ...prefs.labels };
	delete known[id];
	delete labels[id];
	return { known, labels, hidden: prefs.hidden.filter((h) => h !== id) };
}
