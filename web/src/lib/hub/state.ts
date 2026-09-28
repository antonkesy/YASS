import type { PageDto, SessionDto } from './generated/Yass.Core.Hub';
import { SessionState } from './generated/Yass.Core.Domain';

/** Pure reducers over the session map, kept separate from the connection so they are unit-testable. */
export type SessionMap = ReadonlyMap<string, SessionDto>;

/** Server signals removal with a Done session carrying no pages and an Error marker. */
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

/** Newest first, as the home page lists them. */
export function sortedSessions(map: SessionMap): SessionDto[] {
	return [...map.values()].sort((a, b) => b.CreatedAt.localeCompare(a.CreatedAt));
}
