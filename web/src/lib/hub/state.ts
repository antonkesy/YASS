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

// Thumbnails are cached by hash and never removed, so a hash once ready stays ready. A snapshot the
// server built before the render finished can arrive after the PageUpdated that reported it.
function keepReady(known: SessionDto | undefined, page: PageDto): PageDto {
	if (page.ThumbnailReady || !known?.Pages.some((p) => p.ThumbnailReady && p.Hash === page.Hash)) {
		return page;
	}
	return { ...page, ThumbnailReady: true };
}

export function applySessionChanged(map: SessionMap, session: SessionDto): SessionMap {
	const next = new Map(map);
	if (isRemoval(session)) {
		next.delete(session.Id);
	} else {
		const known = map.get(session.Id);
		next.set(session.Id, { ...session, Pages: session.Pages.map((p) => keepReady(known, p)) });
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
	const fresh = keepReady(session, page);
	const pages = session.Pages.map((p) => (p.Index === page.Index ? fresh : p));
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
