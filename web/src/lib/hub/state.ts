import type { PageDto, ScannerStatusDto, SessionDto } from './generated/Yass.Core.Hub';
import { SessionState } from './generated/Yass.Core.Domain';

export type SessionMap = ReadonlyMap<string, SessionDto>;

export function isRemoval(session: SessionDto): boolean {
	return (
		session.State === SessionState.Done &&
		session.Pages.length === 0 &&
		(session.Error === 'deleted' || session.Error === 'expired')
	);
}

export function applySessionChanged(map: SessionMap, session: SessionDto): SessionMap {
	const next = new Map(map);
	if (isRemoval(session)) {
		next.delete(session.Id);
	} else {
		next.set(session.Id, session);
	}
	return next;
}

export function applyPageAdded(map: SessionMap, sessionId: string, page: PageDto): SessionMap {
	const session = map.get(sessionId);
	if (!session) return map;
	const pages = session.Pages.filter((p) => p.Index !== page.Index);
	pages.push(page);
	pages.sort((a, b) => a.Index - b.Index);
	return new Map(map).set(sessionId, { ...session, Pages: pages });
}

export function applyPageUpdated(map: SessionMap, sessionId: string, page: PageDto): SessionMap {
	const session = map.get(sessionId);
	if (!session) return map;
	const pages = session.Pages.map((p) => (p.Index === page.Index ? page : p));
	return new Map(map).set(sessionId, { ...session, Pages: pages });
}

export function replaceAll(sessions: SessionDto[]): SessionMap {
	return new Map(sessions.map((s) => [s.Id, s]));
}

export function sortedSessions(map: SessionMap): SessionDto[] {
	return [...map.values()].sort((a, b) => b.CreatedAt.localeCompare(a.CreatedAt));
}

/** Only State/DeviceName changes mean the device probe saw something new. */
export function devicesStale(
	prev: ScannerStatusDto | null,
	next: ScannerStatusDto | null
): boolean {
	if (!next) return false;
	if (!prev) return true;
	return prev.State !== next.State || prev.DeviceName !== next.DeviceName;
}
